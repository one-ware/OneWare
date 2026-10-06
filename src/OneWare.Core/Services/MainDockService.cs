using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Core.Dock;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.Views.Windows;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ProjectExplorer.Services;

namespace OneWare.Core.Services;

public class MainDockService : Factory, IMainDockService
{
    private readonly IDockSerializer _serializer;
    private readonly Dictionary<string, ObservableCollection<OneWareUiExtension>> _documentViewExtensions = new();
    private readonly Dictionary<string, Type> _documentViewRegistrations = new();
    private readonly Dictionary<string, Func<string, bool>> _fileOpenOverwrites = new();
    private readonly MainDocumentDockViewModel _mainDocumentDockViewModel;
    
    private readonly IFileWatchService _fileWatchService;
    private readonly IPaths _paths;
    private readonly WelcomeScreenViewModel _welcomeScreenViewModel;

    public readonly Dictionary<DockShowLocation, List<Type>> LayoutRegistrations = new();

    // Layout extensions the current layout has already received. Only extensions missing here are added
    // automatically, so dockables the user closed are not added back on the next start.
    private readonly HashSet<string> _knownLayoutExtensions = new();

    private IDisposable? _lastSub;

    public MainDockService(ICompositeServiceProvider serviceProvider, IPaths paths, IWindowService windowService,
        IApplicationStateService applicationStateService,
        WelcomeScreenViewModel welcomeScreenViewModel, IFileWatchService fileWatchService,
        MainDocumentDockViewModel mainDocumentDockViewModel, ILogger logger)
    {
        _paths = paths;
        _welcomeScreenViewModel = welcomeScreenViewModel;
        _mainDocumentDockViewModel = mainDocumentDockViewModel;
        _fileWatchService = fileWatchService;
        _serializer = new OneWareDockSerializer(serviceProvider, logger);

        _documentViewRegistrations.Add("*", typeof(EditViewModel));

        windowService.RegisterMenuItem("MainWindow_MainMenu/View",
            new MenuItemModel("ResetLayout")
            {
                Header = "Reset Layout",
                Command = new RelayCommand(ResetLayout)
            }
        );

        applicationStateService.RegisterShutdownTask(async () =>
        {
            var unsavedFiles = new List<IExtendedDocument>();

            foreach (var tab in OpenFiles)
                if (tab.Value is { IsDirty: true } evm)
                    unsavedFiles.Add(evm);

            var shutdownReady =
                await WindowHelper.HandleUnsavedFilesAsync(unsavedFiles,
                    ContainerLocator.Container.Resolve<MainWindow>());

            if (shutdownReady)
            {
                SaveLayout();

                foreach (var tab in OpenFiles.Values.OfType<ExtendedDocument>()) tab.IsDirty = false;
            }

            return shutdownReady;
        });
    }

    public Dictionary<string, IExtendedDocument> OpenFiles { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    public RootDock? Layout
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Layout)));

            _lastSub?.Dispose();
            _lastSub = field?.WhenValueChanged(c => c.FocusedDockable).Subscribe(y =>
            {
                if (field.FocusedDockable is IExtendedDocument ed) CurrentDocument = ed;
            });
        }
    }

    public IExtendedDocument? CurrentDocument
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDocument)));
        }
    }
    
    public override void FloatDockable(IDockable dockable)
    {
        // Blocking this for now to make sure we don't float pinned dockables in a way where they get duplicated
        if (dockable is ToolDock { ActiveDockable: { } ad })
        {
            var pinned = Layout?.RightPinnedDockables?.Contains(ad) ?? Layout?.LeftPinnedDockables?.Contains(ad) ??
                Layout?.TopPinnedDockables?.Contains(ad) ??
                Layout?.BottomPinnedDockables?.Contains(ad) ?? false;

            if (pinned) return;
        }
        base.FloatDockable(dockable);
    }

    public override void CloseDockable(IDockable? dockable)
    {
        // When closing a floating window in Windows, the method might get called with a null dockable
        // This is likely a bug with the dock library
        // We check it to prevent a crash
        if (dockable == null) return;
        
        try
        {
            base.CloseDockable(dockable);
        }
        catch (Exception e)
        {
            // Dock's collapse logic throws a NullReferenceException when the last
            // dockable of a floating window is closed (issue #257). Recover instead of
            // letting the exception bubble up and crash the whole application.
            ContainerLocator.Container.Resolve<ILogger>()
                ?.Warning("Error while closing dockable, recovering floating window state", e);

            SafeRemoveDockable(dockable);
            CloseEmptyFloatingWindows();
        }
    }

    private void SafeRemoveDockable(IDockable? dockable)
    {
        if (dockable?.Owner is IDock { VisibleDockables: { } dockables } &&
            dockables.Contains(dockable))
        {
            dockables.Remove(dockable);
            OnDockableClosed(dockable);
        }
    }

    private void CloseEmptyFloatingWindows()
    {
        if (Layout?.Windows == null) return;

        foreach (var window in Layout.Windows.ToList())
        {
            if (window.Layout is { } layout && HasContentDockable(layout)) continue;

            try
            {
                window.Exit();
            }
            catch
            {
                RemoveWindow(window);
            }
        }
    }

    private static bool HasContentDockable(IDockable dockable)
    {
        if (dockable is ITool or IDocument) return true;
        if (dockable is IDock { VisibleDockables: { } visibleDockables })
            return visibleDockables.Any(HasContentDockable);
        return false;
    }

    public void RegisterDocumentView<T>(params string[] extensions) where T : IExtendedDocument
    {
        foreach (var extension in extensions) _documentViewRegistrations.TryAdd(extension, typeof(T));
    }

    public void RegisterFileOpenOverwrite(Func<string, bool> action, params string[] extensions)
    {
        foreach (var extension in extensions) _fileOpenOverwrites.TryAdd(extension, action);
    }

    public void RegisterLayoutExtension<T>(DockShowLocation location)
    {
        LayoutRegistrations.TryAdd(location, new List<Type>());
        LayoutRegistrations[location].Add(typeof(T));

        // Modules initialized after the layout was loaded (e.g. plugins installed at runtime)
        // need their dockable added to the open layout right away.
        if (Layout == null) return;

        void AddToLayout()
        {
            if (Layout == null || !_knownLayoutExtensions.Add(GetLayoutExtensionKey(typeof(T)))) return;
            try
            {
                if (AddRegisteredDockable(location, typeof(T), SearchAllDockables(Layout).ToList(), true) is
                    IWaitForContent wC)
                    wC.InitializeContent();
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()
                    ?.Warning($"Could not add {typeof(T).Name} to the current layout", e);
            }
        }

        if (Dispatcher.UIThread.CheckAccess()) AddToLayout();
        else Dispatcher.UIThread.Post(AddToLayout);
    }

    public async Task<IExtendedDocument?> OpenFileAsync(string fullPath)
    {
        var extension = Path.GetExtension(fullPath);
        if (_fileOpenOverwrites.TryGetValue(extension, out var overwrite))
            // If overwrite executes successfully, return null
            // This means that the file is open in an external program
            if (overwrite.Invoke(fullPath))
                return null;

        var fileKey = fullPath.ToPathKey();
        if (OpenFiles.ContainsKey(fileKey))
        {
            Show(OpenFiles[fileKey]);

            return OpenFiles[fileKey];
        }

        _documentViewRegistrations.TryGetValue(extension, out var type);
        type ??= typeof(EditViewModel);
        var viewModel = ContainerLocator.Current.Resolve(type, (typeof(string), fullPath)) as IExtendedDocument;

        if (viewModel == null) throw new NullReferenceException($"{type} could not be resolved!");

        Show(viewModel, DockShowLocation.Document);

        if (_mainDocumentDockViewModel.VisibleDockables?.Contains(_welcomeScreenViewModel) ?? false)
            _mainDocumentDockViewModel.VisibleDockables.Remove(_welcomeScreenViewModel);

        if (viewModel is EditViewModel evm) await evm.WaitForEditorReadyAsync();

        return viewModel;
    }

    public async Task<bool> CloseFileAsync(string fullPath)
    {
        var fileKey = fullPath.ToPathKey();
        if (OpenFiles.ContainsKey(fileKey))
        {
            var vm = OpenFiles[fileKey];
            if (vm.IsDirty && !await vm.TryCloseAsync()) return false;
            OpenFiles.Remove(fileKey);
            CloseDockable(vm);
            _fileWatchService.UnregisterSingleFile(fullPath);
        }

        return true;
    }

    public void UnregisterOpenFile(string fullPath)
    {
        _fileWatchService.UnregisterSingleFile(fullPath);
    }

    public Window? GetWindowOwner(IDockable? dockable)
    {
        while (dockable != null)
        {
            if (dockable is IRootDock { Window.Host: Window host }) return host;
            dockable = dockable.Owner;
        }

        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            ? ContainerLocator.Container.Resolve<MainWindow>()
            : null;
    }

    /// <summary>
    /// Returns the floating dock window hosting the given dockable, or null when it lives in the main window.
    /// </summary>
    private static Window? GetFloatingWindowOwner(IDockable? dockable)
    {
        while (dockable != null)
        {
            if (dockable is IRootDock { Window.Host: Window host }) return host;
            dockable = dockable.Owner;
        }

        return null;
    }

    public IDockable? SearchView(IDockable instance, IDockable? layout = null)
    {
        layout ??= Layout;

        if (layout is IDock { VisibleDockables: not null } dock)
            foreach (var dockable in dock.VisibleDockables)
            {
                if (dockable is IDock sub)
                    if (SearchView(instance, sub) is { } result)
                        return result;
                if (dockable == instance) return dockable;
            }

        if (layout is not IRootDock { Windows: not null } rootDock) return null;
        foreach (var win in rootDock.Windows)
            if (SearchView(instance, win.Layout) is { } result)
                return result;

        return null;
    }

    public IEnumerable<T> SearchView<T>(IDockable? layout = null)
    {
        layout ??= Layout;

        if (layout is IDock { VisibleDockables: not null } dock)
            foreach (var dockable in dock.VisibleDockables)
            {
                if (dockable is IDock sub)
                    foreach (var bs in SearchView<T>(sub))
                        yield return bs;
                if (dockable is T tx)
                    yield return tx;
            }

        if (layout is not IRootDock rootDock) yield break;

        if (rootDock.LeftPinnedDockables != null)
            foreach (var dockable in rootDock.LeftPinnedDockables)
            {
                if (dockable is IDock sub)
                    foreach (var bs in SearchView<T>(sub))
                        yield return bs;
                if (dockable is T tx)
                    yield return tx;
            }

        if (rootDock.TopPinnedDockables != null)
            foreach (var dockable in rootDock.TopPinnedDockables)
            {
                if (dockable is IDock sub)
                    foreach (var bs in SearchView<T>(sub))
                        yield return bs;
                if (dockable is T tx)
                    yield return tx;
            }

        if (rootDock.RightPinnedDockables != null)
            foreach (var dockable in rootDock.RightPinnedDockables)
            {
                if (dockable is IDock sub)
                    foreach (var bs in SearchView<T>(sub))
                        yield return bs;
                if (dockable is T tx)
                    yield return tx;
            }

        if (rootDock.BottomPinnedDockables != null)
            foreach (var dockable in rootDock.BottomPinnedDockables)
            {
                if (dockable is IDock sub)
                    foreach (var bs in SearchView<T>(sub))
                        yield return bs;
                if (dockable is T tx)
                    yield return tx;
            }

        if (rootDock.Windows != null)
            foreach (var win in rootDock.Windows)
            {
                foreach (var c in SearchView<T>(win.Layout)) yield return c;
                if (win is T x)
                    yield return x;
            }
    }

    public override void OnDockableClosed(IDockable? dockable)
    {
        base.OnDockableClosed(dockable);
        if (dockable == CurrentDocument) CurrentDocument = null;
    }

    public void ResetLayout()
    {
        LoadLayout("Default", true);
    }

    public override void InitLayout(IDockable layout)
    {
        OpenFiles.Clear();

        ContextLocator = new Dictionary<string, Func<object?>>();
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => ContainerLocator.Container!.Resolve<AdvancedHostWindow>()
        };
        DockableLocator = new Dictionary<string, Func<IDockable?>>();

        base.InitLayout(layout);
    }

    #region ShowWindows

    public void Show<T>(DockShowLocation location = DockShowLocation.Window) where T : IDockable
    {
        Show(ContainerLocator.Container.Resolve<T>(), location);
    }

    public void Show(IDockable dockable, DockShowLocation location = DockShowLocation.Window)
    {
        if (IsDockablePinned(dockable))
        {
            PreviewPinnedDockable(dockable);
            return;
        }

        //Check if dockable already exists
        if (SearchView(dockable) is { } result)
        {
            SetActiveDockable(result);
            // Only floating dock windows are raised here. Activating the main window would
            // steal focus from whatever the user is doing whenever a dockable is shown
            // programmatically (background tools, AI functions, ...).
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                var ownerWindow = GetFloatingWindowOwner(dockable);
                if (ownerWindow != null) Dispatcher.UIThread.Post(ownerWindow.Activate);
            }

            return;
        }

        if (location == DockShowLocation.Document)
        {
            _mainDocumentDockViewModel.VisibleDockables?.Add(dockable);
            InitActiveDockable(dockable, _mainDocumentDockViewModel);
            SetActiveDockable(dockable);
        }
        else if (location == DockShowLocation.Left || location == DockShowLocation.Right || location == DockShowLocation.Bottom)
        {
            // Find the appropriate tool dock based on location
            var toolDock = FindOrCreateToolDock(location);
            if (toolDock != null)
            {
                // Use AddDockable (not a raw VisibleDockables.Add) so the dock's
                // IsEmpty flag and owner chain are updated. A raw add leaves the
                // tool dock flagged empty and it renders with zero size (issue #258).
                AddDockable(toolDock, dockable);
                SetActiveDockable(dockable);
            }
            else
            {
                // Fallback to window if tool dock not found
                ShowAsWindow(dockable);
            }
        }
        else if (location == DockShowLocation.LeftPinned || location == DockShowLocation.RightPinned)
        {
            // Handle pinned dockables
            AddPinnedDockable(dockable, location);
        }
        else if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            ShowAsWindow(dockable);
        }

        if (dockable is IWaitForContent wC) wC.InitializeContent();
    }

    private void ShowAsWindow(IDockable dockable)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = CreateWindowFrom(dockable);

            if (Layout == null) throw new NullReferenceException(nameof(Layout));

            if (window != null)
            {
                var mainWindow = ContainerLocator.Current.Resolve<MainWindow>();
                AddWindow(Layout, window);
                window.Height = 400;
                window.Width = 600;
                window.X = mainWindow.Position.X + mainWindow.Width / 2 - window.Width / 2;
                window.Y = mainWindow.Position.Y + mainWindow.Height / 2 - window.Height / 2;
                window.Topmost = false;
                window.Present(false);
                SetActiveDockable(dockable);
                if (window.Host is Window win) win.Topmost = false;
            }
        });
    }

    private ToolDock? FindOrCreateToolDock(DockShowLocation location)
    {
        if (Layout == null) return null;

        var dockId = GetToolDockId(location);

        if (dockId == null) return null;

        // Search for the tool dock
        var toolDock = SearchView<ToolDock>().FirstOrDefault(t => t.Id == dockId);

        // If not found, try to create it
        if (toolDock == null)
        {
            toolDock = CreateToolDockForLocation(location, dockId);
        }

        return toolDock;
    }

    private static string? GetToolDockId(DockShowLocation location)
    {
        return location switch
        {
            DockShowLocation.Left => "LeftPaneTop",
            DockShowLocation.Bottom => "BottomPaneOne",
            DockShowLocation.Right => "RightPaneTop",
            _ => null
        };
    }

    private ToolDock? CreateToolDockForLocation(DockShowLocation location, string dockId)
    {
        if (Layout == null) return null;

        var alignment = location switch
        {
            DockShowLocation.Left => Alignment.Left,
            DockShowLocation.Bottom => Alignment.Bottom,
            DockShowLocation.Right => Alignment.Right,
            _ => Alignment.Unset
        };

        if (alignment == Alignment.Unset) return null;

        var toolDock = new ToolDock
        {
            Id = dockId,
            Title = dockId,
            Proportion = double.NaN,
            VisibleDockables = CreateList<IDockable>(),
            Alignment = alignment
        };

        // Find the parent proportional dock
        var parentDockId = location switch
        {
            DockShowLocation.Left => "LeftPane",
            DockShowLocation.Bottom => "BottomRow",
            // "RightPane" is the center column (documents + bottom row), so the
            // right-side tool column needs its own id.
            DockShowLocation.Right => "RightSidePane",
            _ => null
        };

        if (parentDockId != null)
        {
            var parentDock = SearchView<ProportionalDock>().FirstOrDefault(p => p.Id == parentDockId);
            if (parentDock == null)
            {
                // Create the parent proportional dock if it doesn't exist
                parentDock = CreateProportionalDockForLocation(location, parentDockId);
            }

            if (parentDock != null)
            {
                // AddDockable updates IsEmpty and the owner chain; a raw add leaves
                // the parent flagged empty so it renders with zero size (issue #258).
                AddDockable(parentDock, toolDock);
                return toolDock;
            }
        }

        return null;
    }

    private ProportionalDock? CreateProportionalDockForLocation(DockShowLocation location, string dockId)
    {
        if (Layout == null) return null;

        var proportion = location switch
        {
            DockShowLocation.Left => 0.25,
            DockShowLocation.Bottom => 0.3,
            DockShowLocation.Right => 0.25,
            _ => double.NaN
        };

        Orientation? orientation = location switch
        {
            DockShowLocation.Left => Orientation.Vertical,
            DockShowLocation.Bottom => Orientation.Horizontal,
            DockShowLocation.Right => Orientation.Vertical,
            _ => null
        };

        if (orientation == null) return null;

        var proportionalDock = new ProportionalDock
        {
            Id = dockId,
            Title = dockId,
            Proportion = proportion,
            Orientation = orientation.Value,
            VisibleDockables = CreateList<IDockable>()
        };

        // Try to insert into main layout
        var mainLayout = SearchView<ProportionalDock>().FirstOrDefault(p => p.Id == "MainLayout");
        if (mainLayout != null)
        {
            // Track the dock the new proportional dock is actually parented under so
            // its Owner is initialized correctly. Passing the wrong owner here leaves
            // the docking tree inconsistent and breaks subsequent open/close cycles.
            ProportionalDock? actualParent = null;

            if (location == DockShowLocation.Left)
            {
                // Existing siblings should auto-distribute the remaining space; leaving
                // a stale proportion on them (or 0 on the new dock) makes the pane
                // render with zero width (issue #258).
                ResetChildProportions(mainLayout);
                InsertDockable(mainLayout, proportionalDock, 0);
                if (mainLayout.VisibleDockables?.Count > 1)
                    InsertDockable(mainLayout, new ProportionalDockSplitter(), 1);

                actualParent = mainLayout;
            }
            else if (location == DockShowLocation.Bottom)
            {
                // Find RightPane and add bottom dock to it
                var rightPane = SearchView<ProportionalDock>().FirstOrDefault(p => p.Id == "RightPane");
                if (rightPane != null)
                {
                    ResetChildProportions(rightPane);
                    AddDockable(rightPane, new ProportionalDockSplitter());
                    AddDockable(rightPane, proportionalDock);
                    actualParent = rightPane;
                }
            }
            else if (location == DockShowLocation.Right)
            {
                // Keep the left pane's width; only let the center column and any
                // collapsed siblings share the remaining space.
                if (mainLayout.VisibleDockables != null)
                    foreach (var child in mainLayout.VisibleDockables)
                        if (child is not IProportionalDockSplitter &&
                            (child.Id == "RightPane" || !(child.Proportion > 0)))
                            child.Proportion = double.NaN;
                AddDockable(mainLayout, new ProportionalDockSplitter());
                AddDockable(mainLayout, proportionalDock);
                actualParent = mainLayout;
            }

            if (actualParent != null)
                return proportionalDock;
        }

        return null;
    }

    /// <summary>
    /// Resets the proportion of a dock's non-splitter children to NaN so the
    /// proportional layout redistributes space evenly. Without this, a stale or
    /// zero proportion left over from a collapsed dock causes a newly inserted
    /// sibling to render with zero size.
    /// </summary>
    private static void ResetChildProportions(ProportionalDock dock)
    {
        if (dock.VisibleDockables == null) return;
        foreach (var child in dock.VisibleDockables)
            if (child is not IProportionalDockSplitter)
                child.Proportion = double.NaN;
    }

    private void AddPinnedDockable(IDockable dockable, DockShowLocation location)
    {
        if (Layout is not RootDock rootDock) return;

        dockable.Proportion = 0.3;
        dockable.PinnedBounds = null;

        switch (location)
        {
            case DockShowLocation.LeftPinned:
                rootDock.LeftPinnedDockables?.Add(dockable);
                break;
            case DockShowLocation.RightPinned:
                rootDock.RightPinnedDockables?.Add(dockable);
                break;
        }
        
        InitDockable(dockable, rootDock);
        PreviewPinnedDockable(dockable);
        
        // After PreviewPinnedDockable, the dockable has been moved into rootDock.PinnedDock.
        // Set focus on PinnedDock (not rootDock) so the panel is interactive (IsActive = true)
        // without stealing the ActiveDockable of the root layout.
        // This fixes an issue where dockables can be grayed out if served from a freshly installed extension
        if (rootDock.PinnedDock is { } pinnedDock)
            SetFocusedDockable(pinnedDock, dockable);
    }

    #endregion


    #region LayoutLoading

    public void LoadLayout(string name, bool reset = false)
    {
        RootDock? layout = null;
        var wasLoadedFromFile = false;

        if (!reset && layout == null) //Docking system load
            try
            {
                var layoutPath = Path.Combine(_paths.LayoutDirectory, name + ".json");
                if (File.Exists(layoutPath))
                {
                    using var stream = File.OpenRead(layoutPath);
                    layout = _serializer.Load<RootDock>(stream);
                    wasLoadedFromFile = true;
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()
                    ?.Warning("Could not load layout from file! Loading default layout...", e);
            }

        if (layout == null)
        {
            layout = name switch
            {
                _ => DefaultLayout.GetDefaultLayout(this)
            };
            OpenFiles.Clear();
            Show(_welcomeScreenViewModel, DockShowLocation.Document);
        }

        layout.Id = name;

        // A default layout already contains every registered extension
        _knownLayoutExtensions.Clear();
        if (wasLoadedFromFile) LoadKnownLayoutExtensions(name);

        // Drop dockables that failed to deserialize (e.g. types from an
        // uninstalled or renamed plugin). Their JSON $type cannot be resolved and
        // the serializer leaves null entries in the layout. Passing those to
        // InitLayout throws a NullReferenceException and prevents the app from
        // starting. Removing them lets the rest of the saved layout load.
        RemoveInvalidDockables(layout);
        var misplacedRightTools = DetachMisplacedRightToolDock(layout);

        try
        {
            InitLayout(layout);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()
                ?.Warning("Could not initialize saved layout! Loading default layout...", e);

            // The saved layout is corrupted beyond repair, fall back to default.
            layout = DefaultLayout.GetDefaultLayout(this);
            layout.Id = name;
            OpenFiles.Clear();
            Show(_welcomeScreenViewModel, DockShowLocation.Document);
            InitLayout(layout);
            Layout = layout;
            MarkLayoutRegistrationsKnown();
            return;
        }

        Layout = layout;

        foreach (var tool in misplacedRightTools)
            Show(tool, DockShowLocation.Right);
        
        // Only merge registrations if layout was loaded from file (to add new plugins)
        // Skip if it's a fresh default layout (already has everything)
        if (wasLoadedFromFile)
        {
            MergeLayoutRegistrations();
        }

        MarkLayoutRegistrationsKnown();
    }

    private static string GetLayoutExtensionKey(Type type)
    {
        return type.FullName ?? type.Name;
    }

    private string GetKnownLayoutExtensionsPath(string layoutId)
    {
        return Path.Combine(_paths.LayoutDirectory, layoutId + ".extensions.json");
    }

    private void MarkLayoutRegistrationsKnown()
    {
        foreach (var type in LayoutRegistrations.Values.SelectMany(x => x))
            _knownLayoutExtensions.Add(GetLayoutExtensionKey(type));
    }

    /// <summary>
    /// Loads the layout extensions a saved layout has already received. If the file is missing
    /// (layouts saved by older versions), no extension is known and missing dockables are added once.
    /// </summary>
    private void LoadKnownLayoutExtensions(string layoutId)
    {
        try
        {
            var path = GetKnownLayoutExtensionsPath(layoutId);
            if (!File.Exists(path)) return;

            using var stream = File.OpenRead(path);
            if (JsonSerializer.Deserialize<string[]>(stream) is { } known)
                _knownLayoutExtensions.UnionWith(known);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()
                ?.Warning("Could not load known layout extensions", e);
        }
    }

    private void SaveKnownLayoutExtensions(string layoutId)
    {
        try
        {
            File.WriteAllText(GetKnownLayoutExtensionsPath(layoutId),
                JsonSerializer.Serialize(_knownLayoutExtensions.Order().ToArray()));
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()
                ?.Warning("Could not save known layout extensions", e);
        }
    }

    /// <summary>
    /// Recursively removes invalid dockables from a layout tree. These are null
    /// entries and <see cref="MissingDockable"/> placeholders left when a saved
    /// dockable references a type that can no longer be resolved (for example a
    /// plugin that was uninstalled or renamed). The placeholder is kept during
    /// deserialization so the shared structural tree nested inside it survives, then
    /// stripped here once the layout has been reconstructed.
    /// </summary>
    private static void RemoveInvalidDockables(IDockable? dockable)
    {
        if (dockable is IRootDock rootDock)
        {
            RemoveNullEntries(rootDock.LeftPinnedDockables);
            RemoveNullEntries(rootDock.RightPinnedDockables);
            RemoveNullEntries(rootDock.TopPinnedDockables);
            RemoveNullEntries(rootDock.BottomPinnedDockables);
            RemoveNullEntries(rootDock.HiddenDockables);

            if (rootDock.Windows != null)
                foreach (var window in rootDock.Windows)
                    RemoveInvalidDockables(window.Layout);
        }

        if (dockable is IDock { VisibleDockables: { } visibleDockables } dock)
        {
            RemoveNullEntries(visibleDockables);
            foreach (var child in dock.VisibleDockables!.ToList())
                RemoveInvalidDockables(child);
        }
    }

    /// <summary>
    /// Older versions attached the right tool dock ("RightPaneTop") to the center
    /// column ("RightPane"), where it rendered with zero height. Removes such a dock
    /// from a saved layout and returns its tools so they can be re-shown correctly.
    /// </summary>
    private static List<IDockable> DetachMisplacedRightToolDock(IDockable layout)
    {
        var result = new List<IDockable>();
        var centerColumns = new List<IDock>();
        CollectDocks(layout, "RightPane", centerColumns);

        foreach (var center in centerColumns)
        {
            var list = center.VisibleDockables!;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is not IDock { Id: "RightPaneTop" } misplaced) continue;
                if (misplaced.VisibleDockables != null) result.AddRange(misplaced.VisibleDockables);
                list.RemoveAt(i);
                if (i > 0 && i - 1 < list.Count && list[i - 1] is IProportionalDockSplitter)
                    list.RemoveAt(i - 1);
            }
        }

        return result;
    }

    private static void CollectDocks(IDockable? dockable, string id, List<IDock> result)
    {
        if (dockable is not IDock { VisibleDockables: { } children } dock) return;
        if (dock.Id == id) result.Add(dock);
        foreach (var child in children)
            CollectDocks(child, id, result);
    }

    private static void RemoveNullEntries(IList<IDockable>? list)
    {
        if (list == null) return;
        for (var i = list.Count - 1; i >= 0; i--)
            if (list[i] is null or MissingDockable)
                list.RemoveAt(i);
    }
    
    private void MergeLayoutRegistrations()
    {
        if (Layout == null) return;

        // Get all existing dockables in the layout to avoid duplicates
        var existingDockables = SearchAllDockables(Layout).ToList();

        foreach (var (location, types) in LayoutRegistrations)
        foreach (var type in types.ToList())
            if (!_knownLayoutExtensions.Contains(GetLayoutExtensionKey(type)))
                AddRegisteredDockable(location, type, existingDockables, false);
    }

    /// <summary>
    /// Adds a dockable registered with <see cref="RegisterLayoutExtension{T}"/> to the current layout,
    /// unless a dockable of that type is already part of it.
    /// </summary>
    /// <returns>The dockable that was added, or null if nothing was added.</returns>
    private IDockable? AddRegisteredDockable(DockShowLocation location, Type type, List<IDockable> existingDockables,
        bool createToolDock)
    {
        if (Layout == null) return null;

        // Check if any existing dockable is assignable to this type
        // This handles both concrete types and interfaces/base classes
        if (existingDockables.Any(type.IsInstanceOfType)) return null;

        switch (location)
        {
            case DockShowLocation.Left or DockShowLocation.Bottom or DockShowLocation.Right:
            {
                var toolDock = createToolDock
                    ? FindOrCreateToolDock(location)
                    : SearchView<ToolDock>().FirstOrDefault(t => t.Id == GetToolDockId(location));
                if (toolDock == null) return null;
                if (ContainerLocator.Container.Resolve(type) is not IDockable dockable) return null;

                // AddDockable (not a raw VisibleDockables.Add) initializes the owner chain and IsEmpty (issue #258).
                AddDockable(toolDock, dockable);
                toolDock.ActiveDockable ??= dockable;
                existingDockables.Add(dockable);
                return dockable;
            }
            case DockShowLocation.LeftPinned or DockShowLocation.RightPinned:
            {
                if (Layout is not RootDock rootDock) return null;
                var pinnedList = location == DockShowLocation.LeftPinned
                    ? rootDock.LeftPinnedDockables
                    : rootDock.RightPinnedDockables;
                if (pinnedList == null) return null;
                if (ContainerLocator.Container.Resolve(type) is not IDockable dockable) return null;

                dockable.Proportion = 0.3;
                dockable.PinnedBounds = null;
                pinnedList.Add(dockable);
                InitDockable(dockable, rootDock);
                existingDockables.Add(dockable);
                return dockable;
            }
        }

        return null;
    }

    private IEnumerable<IDockable> SearchAllDockables(IDockable? layout)
    {
        if (layout is IDock { VisibleDockables: not null } dock)
        {
            foreach (var dockable in dock.VisibleDockables)
            {
                yield return dockable;
                if (dockable is IDock sub)
                {
                    foreach (var child in SearchAllDockables(sub))
                        yield return child;
                }
            }
        }

        if (layout is IRootDock rootDock)
        {
            if (rootDock.LeftPinnedDockables != null)
                foreach (var dockable in rootDock.LeftPinnedDockables)
                    yield return dockable;

            if (rootDock.TopPinnedDockables != null)
                foreach (var dockable in rootDock.TopPinnedDockables)
                    yield return dockable;

            if (rootDock.RightPinnedDockables != null)
                foreach (var dockable in rootDock.RightPinnedDockables)
                    yield return dockable;

            if (rootDock.BottomPinnedDockables != null)
                foreach (var dockable in rootDock.BottomPinnedDockables)
                    yield return dockable;

            if (rootDock.Windows != null)
            {
                foreach (var win in rootDock.Windows)
                {
                    if (win.Layout != null)
                        foreach (var child in SearchAllDockables(win.Layout))
                            yield return child;
                }
            }
        }
    }

    public void SaveLayout()
    {
        if (Layout == null) return;

        Directory.CreateDirectory(_paths.LayoutDirectory);

        using var stream = File.OpenWrite(Path.Combine(_paths.LayoutDirectory, Layout.Id + ".json"));
        stream.SetLength(0);

        Layout.FocusedDockable = null;

        _serializer.Save(stream, Layout);

        SaveKnownLayoutExtensions(Layout.Id);
    }

    public void InitializeContent()
    {
        if (_mainDocumentDockViewModel.VisibleDockables?.Count == 0)
        {
            _mainDocumentDockViewModel.AddDocument(_welcomeScreenViewModel);
        }

        var extendedDocs = SearchView<IWaitForContent>();
        foreach (var extendedDocument in extendedDocs)
            extendedDocument.InitializeContent();

        //the current document won't be set during initialization because the 
        //focus event does not necessarily get triggered
        CurrentDocument = _mainDocumentDockViewModel.ActiveDockable as IExtendedDocument;
    }

    #endregion
}
