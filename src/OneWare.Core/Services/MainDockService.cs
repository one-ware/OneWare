using System.Collections.ObjectModel;
using System.ComponentModel;
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

    private IDisposable? _lastSub;

    public MainDockService(ICompositeServiceProvider serviceProvider, IPaths paths, IWindowService windowService,
        IApplicationStateService applicationStateService,
        WelcomeScreenViewModel welcomeScreenViewModel, IFileWatchService fileWatchService,
        MainDocumentDockViewModel mainDocumentDockViewModel)
    {
        _paths = paths;
        _welcomeScreenViewModel = welcomeScreenViewModel;
        _mainDocumentDockViewModel = mainDocumentDockViewModel;
        _fileWatchService = fileWatchService;
        _serializer = new OneWareDockSerializer(serviceProvider);

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
        if (IsDockablePinned(dockable)) UnpinDockable(dockable);

        //Check if dockable already exists
        if (SearchView(dockable) is { } result)
        {
            SetActiveDockable(result);
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                var ownerWindow = GetWindowOwner(dockable);
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
                toolDock.VisibleDockables?.Add(dockable);
                InitActiveDockable(dockable, toolDock);
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

        // Map location to dock ID
        var dockId = location switch
        {
            DockShowLocation.Left => "LeftPaneTop",
            DockShowLocation.Bottom => "BottomPaneOne",
            DockShowLocation.Right => "RightPaneTop",
            _ => null
        };

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
            VisibleDockables = CreateList<IDockable>(),
            Alignment = alignment
        };

        // Find the parent proportional dock
        var parentDockId = location switch
        {
            DockShowLocation.Left => "LeftPane",
            DockShowLocation.Bottom => "BottomRow",
            DockShowLocation.Right => "RightPane",
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
                parentDock.VisibleDockables?.Add(toolDock);
                InitActiveDockable(toolDock, parentDock);
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
            if (location == DockShowLocation.Left)
            {
                // Insert at the beginning
                mainLayout.VisibleDockables?.Insert(0, proportionalDock);
                if (mainLayout.VisibleDockables?.Count > 1)
                {
                    mainLayout.VisibleDockables.Insert(1, new ProportionalDockSplitter());
                }
            }
            else if (location == DockShowLocation.Bottom || location == DockShowLocation.Right)
            {
                // Find RightPane and add bottom dock to it
                var rightPane = SearchView<ProportionalDock>().FirstOrDefault(p => p.Id == "RightPane");
                if (rightPane != null && location == DockShowLocation.Bottom)
                {
                    rightPane.VisibleDockables?.Add(new ProportionalDockSplitter());
                    rightPane.VisibleDockables?.Add(proportionalDock);
                }
                else if (location == DockShowLocation.Right)
                {
                    mainLayout.VisibleDockables?.Add(new ProportionalDockSplitter());
                    mainLayout.VisibleDockables?.Add(proportionalDock);
                }
            }

            InitActiveDockable(proportionalDock, mainLayout);
            return proportionalDock;
        }

        return null;
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
        
        //InitActiveDockable(dockable, rootDock);
        SetActiveDockable(dockable);
        PreviewPinnedDockable(dockable);
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

        InitLayout(layout);

        Layout = layout;
        
        // Only merge registrations if layout was loaded from file (to add new plugins)
        // Skip if it's a fresh default layout (already has everything)
        if (wasLoadedFromFile)
        {
            MergeLayoutRegistrations();
        }
    }
    
    private void MergeLayoutRegistrations()
    {
        if (Layout == null) return;

        // Get all existing dockables in the layout to avoid duplicates
        var existingDockables = SearchAllDockables(Layout).ToList();

        // Process each location registration
        foreach (var (location, types) in LayoutRegistrations)
        {
            if (types.Count == 0) continue;

            // Find the appropriate tool dock for this location
            var toolDock = location switch
            {
                DockShowLocation.Left => SearchView<ToolDock>().FirstOrDefault(t => t.Id == "LeftPaneTop"),
                DockShowLocation.Bottom => SearchView<ToolDock>().FirstOrDefault(t => t.Id == "BottomPaneOne"),
                DockShowLocation.Right => SearchView<ToolDock>().FirstOrDefault(t => t.Id == "RightPaneTop"),
                _ => null
            };

            if (toolDock != null)
            {
                // Check which registered types are not already in the layout
                foreach (var type in types)
                {
                    // Check if any existing dockable is assignable to this type
                    // This handles both concrete types and interfaces/base classes
                    var existsInLayout = existingDockables.Any(d => type.IsInstanceOfType(d));
                    
                    if (!existsInLayout)
                    {
                        // Resolve and add the dockable
                        if (ContainerLocator.Container.Resolve(type) is IDockable dockable)
                        {
                            toolDock.VisibleDockables?.Add(dockable);
                            InitActiveDockable(dockable, toolDock);
                            
                            // Add to our tracking list to avoid duplicate adds in same session
                            existingDockables.Add(dockable);
                        }
                    }
                }
            }
            else if (location == DockShowLocation.LeftPinned || location == DockShowLocation.RightPinned)
            {
                // Handle pinned dockables
                if (Layout is RootDock rootDock)
                {
                    var pinnedList = location == DockShowLocation.LeftPinned 
                        ? rootDock.LeftPinnedDockables 
                        : rootDock.RightPinnedDockables;

                    if (pinnedList != null)
                    {
                        foreach (var type in types)
                        {
                            // Check if any existing dockable is assignable to this type
                            var existsInLayout = existingDockables.Any(d => type.IsInstanceOfType(d));
                            
                            if (!existsInLayout)
                            {
                                if (ContainerLocator.Container.Resolve(type) is IDockable dockable)
                                {
                                    dockable.Proportion = 0.3;
                                    dockable.PinnedBounds = null;
                                    pinnedList.Add(dockable);
                                    InitActiveDockable(dockable, rootDock);
                                    
                                    // Add to our tracking list to avoid duplicate adds in same session
                                    existingDockables.Add(dockable);
                                }
                            }
                        }
                    }
                }
            }
        }
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
