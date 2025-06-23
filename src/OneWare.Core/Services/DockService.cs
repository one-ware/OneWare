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
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Autofac; // Add Autofac namespace
using System; // For NullReferenceException

namespace OneWare.Core.Services;

public class DockService : Factory, IDockService
{
    private readonly IDockSerializer _serializer;
    private readonly ILogger<DockService> _logger;
    private readonly Dictionary<string, ObservableCollection<UiExtension>> _documentViewExtensions = new();
    private readonly Dictionary<string, Type> _documentViewRegistrations = new();
    private readonly Dictionary<string, Func<IFile, bool>> _fileOpenOverwrites = new();
    private readonly MainDocumentDockViewModel _mainDocumentDockViewModel;
    private readonly MainWindow _mainWindow;
    private readonly AdvancedHostWindow _advancedHostWindow;
    private readonly DefaultLayout _defaultLayout;

    private readonly IPaths _paths;
    private readonly WelcomeScreenViewModel _welcomeScreenViewModel;
    private readonly ILifetimeScope _lifetimeScope; // Autofac's lifetime scope

    public readonly Dictionary<DockShowLocation, List<Type>> LayoutRegistrations = new();

    private IExtendedDocument? _currentDocument;

    private IDisposable? _lastSub;

    private RootDock? _layout;

    public DockService(IPaths paths,
                       ILogger<DockService> logger,
                       IWindowService windowService,
                       WelcomeScreenViewModel welcomeScreenViewModel,
                       MainDocumentDockViewModel mainDocumentDockViewModel,
                       MainWindow mainWindow,
                       AdvancedHostWindow advancedHostWindow,
                       DefaultLayout defaultLayout,
                       ILifetimeScope lifetimeScope) // Changed from IContainerAdapter
    {
        _paths = paths;
        _welcomeScreenViewModel = welcomeScreenViewModel;
        _mainDocumentDockViewModel = mainDocumentDockViewModel;

        // Initialize DockSerializer with an Autofac scope or a custom service provider wrapper
        // Assuming DockSerializer needs a way to resolve types.
        // If DockSerializer's constructor is flexible, you might pass `lifetimeScope.Resolve` as a Func<Type, object>
        // Or you might need to create a custom IServiceProvider adapter around Autofac.
        // For simplicity, let's assume DockSerializer can take an IComponentContext or similar.
        // If DockSerializer truly needed IContainerAdapter, you'd need to re-evaluate its implementation
        // or provide a wrapper for Autofac's IComponentContext.
        // A common pattern is to pass a Func<Type, object> for resolving.
        _serializer = new DockSerializer(typeof(ObservableCollection<>), lifetimeScope); // Pass ILifetimeScope directly

        _logger = logger;
        _mainWindow = mainWindow;
        _advancedHostWindow = advancedHostWindow;
        _documentViewRegistrations.Add("*", typeof(EditViewModel));
        _defaultLayout = defaultLayout; // DefaultLayout should now receive ILifetimeScope in its constructor
        _lifetimeScope = lifetimeScope; // Store the lifetime scope

        windowService.RegisterMenuItem("MainWindow_MainMenu/View",
            new MenuItemViewModel("ResetLayout")
            {
                Header = "Reset Layout",
                Command = new RelayCommand(ResetLayout)
            }
        );
    }

    public Dictionary<IFile, IExtendedDocument> OpenFiles { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    public RootDock? Layout
    {
        get => _layout;
        set
        {
            if (value == _layout) return;
            _layout = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Layout)));

            _lastSub?.Dispose();
            _lastSub = _layout?.WhenValueChanged(c => c.FocusedDockable).Subscribe(y =>
            {
                if (_layout.FocusedDockable is IExtendedDocument ed) CurrentDocument = ed;
            });
        }
    }

    public IExtendedDocument? CurrentDocument
    {
        get => _currentDocument;
        set
        {
            if (value == _currentDocument) return;
            _currentDocument = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDocument)));
        }
    }

    public void RegisterDocumentView<T>(params string[] extensions) where T : IExtendedDocument
    {
        foreach (var extension in extensions) _documentViewRegistrations.TryAdd(extension, typeof(T));
    }

    public void RegisterFileOpenOverwrite(Func<IFile, bool> action, params string[] extensions)
    {
        foreach (var extension in extensions) _fileOpenOverwrites.TryAdd(extension, action);
    }

    public void RegisterLayoutExtension<T>(DockShowLocation location)
    {
        LayoutRegistrations.TryAdd(location, new List<Type>());
        LayoutRegistrations[location].Add(typeof(T));
    }

    public async Task<IExtendedDocument?> OpenFileAsync(IFile pf)
    {
        if (_fileOpenOverwrites.TryGetValue(pf.Extension, out var overwrite))
        {
            // If overwrite executes successfully, return null
            // This means that the file is open in an external program
            if (overwrite.Invoke(pf)) return null;
        }

        if (OpenFiles.ContainsKey(pf))
        {
            Show(OpenFiles[pf]);

            return OpenFiles[pf];
        }

        _documentViewRegistrations.TryGetValue(pf.Extension, out var type);
        type ??= typeof(EditViewModel);

        // Resolve the view model using the Autofac lifetime scope
        // This replaces _defaultLayout.ServiceProvider.GetRequiredService(type)
        var viewModel = _lifetimeScope.Resolve(type) as IExtendedDocument;

        if (viewModel == null) throw new NullReferenceException($"{type} could not be resolved from Autofac lifetime scope!");

        Show(viewModel, DockShowLocation.Document);

        if (_mainDocumentDockViewModel.VisibleDockables?.Contains(_welcomeScreenViewModel) ?? false)
            _mainDocumentDockViewModel.VisibleDockables.Remove(_welcomeScreenViewModel);

        if (viewModel is EditViewModel evm) await evm.WaitForEditorReadyAsync();

        return viewModel;
    }

    public async Task<bool> CloseFileAsync(IFile pf)
    {
        if (OpenFiles.ContainsKey(pf))
        {
            var vm = OpenFiles[pf];
            if (vm.IsDirty && !await vm.TryCloseAsync()) return false;
            OpenFiles.Remove(pf);
            CloseDockable(vm);
        }

        return true;
    }

    public Window? GetWindowOwner(IDockable? dockable)
    {
        while (dockable != null)
        {
            if (dockable is IRootDock { Window.Host: Window host }) return host;
            dockable = dockable.Owner;
        }

        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            ? _mainWindow
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
                    foreach (var b in SearchView<T>(sub))
                        yield return b;
                if (dockable is T t)
                    yield return t;
            }

        if (layout is not IRootDock { Windows: not null } rootDock) yield break;
        foreach (var win in rootDock.Windows)
        {
            foreach (var c in SearchView<T>(win.Layout)) yield return c;
            if (win is T t)
                yield return t;
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
            [nameof(IDockWindow)] = () => _advancedHostWindow
        };
        DockableLocator = new Dictionary<string, Func<IDockable?>>();

        base.InitLayout(layout);
    }

    #region ShowWindows

    public void Show<T>(DockShowLocation location = DockShowLocation.Window) where T : IDockable
    {
        if (Layout == null) throw new NullReferenceException(nameof(Layout));

        // Resolve the container using Autofac lifetime scope
        var container = _lifetimeScope.Resolve<T>();
        if (container == null) throw new NullReferenceException($"Could not resolve type {typeof(T)} from the Autofac lifetime scope.");

        Show(container, location);
    }

    public void Show(IDockable dockable, DockShowLocation location = DockShowLocation.Window)
    {
        if (IsDockablePinned(dockable))
        {
            UnpinDockable(dockable);
        }

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
        else if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var window = CreateWindowFrom(dockable);

                if (Layout == null) throw new NullReferenceException(nameof(Layout));

                if (window != null)
                {

                    AddWindow(Layout, window);
                    window.Height = 400;
                    window.Width = 600;
                    window.X = _mainWindow.Position.X + _mainWindow.Width / 2 - window.Width / 2;
                    window.Y = _mainWindow.Position.Y + _mainWindow.Height / 2 - window.Height / 2;
                    window.Topmost = false;
                    window.Present(false);
                    SetActiveDockable(dockable);
                    if (window.Host is Window win) win.Topmost = false;
                }
            });
        }

        if (dockable is IWaitForContent wC) wC.InitializeContent();
    }

    #endregion


    #region LayoutLoading

    public void LoadLayout(string name, bool reset = false)
    {
        RootDock? layout = null;

        if (!reset && layout == null) //Docking system load
            try
            {
                var layoutPath = Path.Combine(_paths.LayoutDirectory, name + ".json");
                if (File.Exists(layoutPath))
                {
                    using var stream = File.OpenRead(layoutPath);
                    layout = _serializer.Load<RootDock>(stream);
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation("Could not load layout from file! Loading default layout..." + e, ConsoleColor.Red);
            }

        if (layout == null)
        {
            // The DefaultLayout's GetDefaultLayout method should now accept an ILifetimeScope or equivalent
            // so it can create new dockables via Autofac.
            layout = name switch
            {
                _ => _defaultLayout.GetDefaultLayout(this) // Pass 'this' (DockService) which is a Factory
            };
            OpenFiles.Clear();
            Show(_welcomeScreenViewModel, DockShowLocation.Document);
        }

        layout.Id = name;

        InitLayout(layout);

        Layout = layout;
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
        var extendedDocs = SearchView<IWaitForContent>();
        foreach (var extendedDocument in extendedDocs) extendedDocument.InitializeContent();
    }

    #endregion
}

// Custom adapter to make Autofac's ILifetimeScope compatible with IDockSerializer
// assuming IDockSerializer expects an IServiceProvider or a similar resolution mechanism.
// If DockSerializer expects IContainerAdapter, you might need to adjust DockSerializer itself.
public class AutofacServiceProviderAdapter : IServiceProvider // Or a custom interface that DockSerializer expects
{
    private readonly ILifetimeScope _lifetimeScope;

    public AutofacServiceProviderAdapter(ILifetimeScope lifetimeScope)
    {
        _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
    }

    public object? GetService(Type serviceType)
    {
        // Autofac's TryResolve is safer here to avoid exceptions during deserialization
        // if a type can't be found.
        if (_lifetimeScope.TryResolve(serviceType, out var instance))
        {
            return instance;
        }
        return null;
    }
}

// You will also need to update DefaultLayout.cs to accept ILifetimeScope in its constructor
// if it creates view models directly using a service provider.
/*
// Example of how DefaultLayout.cs constructor might look (conceptual)
public class DefaultLayout
{
    private readonly ILifetimeScope _lifetimeScope;

    public DefaultLayout(ILifetimeScope lifetimeScope)
    {
        _lifetimeScope = lifetimeScope;
    }

    // In methods like GetDefaultLayout, you would use _lifetimeScope.Resolve<T>()
    // instead of ServiceProvider.GetRequiredService.
    public RootDock GetDefaultLayout(IFactory factory)
    {
        // Example:
        var solutionExplorer = _lifetimeScope.Resolve<SolutionExplorerViewModel>();
        // ... build your layout using resolved view models
        return new RootDock();
    }
}
*/