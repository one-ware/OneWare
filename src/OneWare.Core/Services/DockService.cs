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
using OneWare.Core.Dock;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.Views.Windows;
using Prism.Ioc;
using OneWare.SDK.Enums;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;

namespace OneWare.Core.Services
{
    public class DockService : Factory, IDockService
    {
        private static readonly IDockSerializer Serializer = new DockSerializer(typeof(ObservableCollection<>));

        public readonly Dictionary<DockShowLocation, List<Type>> LayoutRegistrations = new ();
        private readonly Dictionary<string, Type> _documentViewRegistrations = new ();
        
        private readonly IPaths _paths;
        private readonly WelcomeScreenViewModel _welcomeScreenViewModel;
        private readonly MainDocumentDockViewModel _mainDocumentDockViewModel;
        public Dictionary<IFile,IExtendedDocument> OpenFiles { get; } = new();
        public event PropertyChangedEventHandler? PropertyChanged;

        private IDisposable? _lastSub = null;
        
        private RootDock? _layout;
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDocument)));
                });
            }
        }
        
        public IExtendedDocument? CurrentDocument { get; set; }

        public DockService(IPaths paths, IWindowService windowService, WelcomeScreenViewModel welcomeScreenViewModel, MainDocumentDockViewModel mainDocumentDockViewModel)
        {
            _paths = paths;
            _welcomeScreenViewModel = welcomeScreenViewModel;
            _mainDocumentDockViewModel = mainDocumentDockViewModel;
            
            _documentViewRegistrations.Add("*", typeof(EditViewModel));

            windowService.RegisterMenuItem("MainWindow_MainMenu/View", 
                new MenuItemViewModel("ResetLayout")
                {
                    Header = "Reset Layout",
                    Command = new RelayCommand(ResetLayout),
                }
            );
        }

        public void RegisterDocumentView<T>(params string[] extensions) where T : IExtendedDocument
        {
            foreach (var extension in extensions)
            {
                _documentViewRegistrations.TryAdd(extension, typeof(T));
            }
        }

        public void RegisterLayoutExtension<T>(DockShowLocation location)
        {
            LayoutRegistrations.TryAdd(location, new List<Type>());
            LayoutRegistrations[location].Add(typeof(T));
        }
        
        public async Task<IExtendedDocument> OpenFileAsync(IFile pf)
        {
            if (OpenFiles.ContainsKey(pf))
            {
                Show(OpenFiles[pf]);

                return OpenFiles[pf];
            }

            _documentViewRegistrations.TryGetValue(pf.Extension, out var type);
            type ??= typeof(EditViewModel);
            var viewModel = ContainerLocator.Current.Resolve(type, (typeof(string), pf.FullPath)) as IExtendedDocument;

            if (viewModel == null) throw new NullReferenceException($"{type} could not be resolved!");

            Show(viewModel, DockShowLocation.Document);
            
            if (_mainDocumentDockViewModel.VisibleDockables?.Contains(_welcomeScreenViewModel) ?? false) 
                _mainDocumentDockViewModel.VisibleDockables.Remove(_welcomeScreenViewModel);
            
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
            return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime ? ContainerLocator.Container.Resolve<MainWindow>() : null;
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
                [nameof(IDockWindow)] = () => ContainerLocator.Container.Resolve<AdvancedHostWindow>()
            };
            DockableLocator = new Dictionary<string, Func<IDockable?>>();

            base.InitLayout(layout);
        }
        
        public IDockable? SearchView(IDockable instance, IDockable? layout = null)
        {
            layout ??= Layout;

            if (layout is IDock {VisibleDockables: not null} dock)
            {
                foreach (var dockable in dock.VisibleDockables)
                {
                    if (dockable is IDock sub)
                    {
                        if (SearchView(instance, sub) is { } result) return result;
                    }
                    if (dockable == instance) return dockable;
                }
            }

            if (layout is not IRootDock {Windows: not null} rootDock) return null;
            foreach (var win in rootDock.Windows)
            {
                if (SearchView(instance, win.Layout) is { } result) return result;
            }

            return null;
        }

        public IEnumerable<T> SearchView<T>(IDockable? layout = null)
        {
            layout ??= Layout;

            if (layout is IDock {VisibleDockables: not null} dock)
            {
                foreach (var dockable in dock.VisibleDockables)
                {
                    if (dockable is IDock sub)
                    {
                        foreach (var b in SearchView<T>(sub))
                        {
                            yield return b;
                        }
                    }
                    if (dockable is T t)
                        yield return t;
                }
            }

            if (layout is not IRootDock { Windows: not null } rootDock) yield break;
            foreach (var win in rootDock.Windows)
            {
                foreach (var c in  SearchView<T>(win.Layout))
                {
                    yield return c;
                }
                if (win is T t)
                    yield return t;
            }
        }

        #region ShowWindows

        public void Show<T>(DockShowLocation location = DockShowLocation.Window) where T : IDockable
        {
            Show(ContainerLocator.Container.Resolve<T>(), location);
        }
        
        public void Show(IDockable dockable, DockShowLocation location = DockShowLocation.Window)
        {
            //Check if dockable already exists
            if (SearchView(dockable) is { } result)
            {
                SetActiveDockable(result);
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
                {
                    var ownerWindow = GetWindowOwner(dockable);
                    if(ownerWindow != null) Dispatcher.UIThread.Post(ownerWindow.Activate);
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
                        var mainWindow = ContainerLocator.Current.Resolve<MainWindow>();
                        AddWindow(Layout, window);
                        window.Height = 400;
                        window.Width = 600;
                        window.X = mainWindow.Position.X + mainWindow.Width / 2 - window.Width / 2;
                        window.Y = mainWindow.Position.Y + mainWindow.Height / 2 - window.Height / 2;
                        window.Topmost = false;
                        window.Present(false);
                        SetActiveDockable(dockable);
                        if (window.Host is Window win)
                        {
                            win.Topmost = false;
                        }
                    }
                });
            }
            if (dockable is IWaitForContent wC)
            {
                wC.InitializeContent();
            }
        }

        #endregion
        
        
        #region LayoutLoading

        public void LoadLayout(string name, bool reset = false)
        {
            RootDock? layout = null;

            if (!reset && layout == null) //Docking system load
            {
                try
                {
                    var layoutPath = Path.Combine(_paths.LayoutDirectory, name + ".json");
                    if (File.Exists(layoutPath))
                    {
                        using var stream = File.OpenRead(layoutPath);
                        layout = Serializer.Load<RootDock>(stream);
                    }
                }
                catch(Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Log("Could not load layout from file! Loading default layout..." + e , ConsoleColor.Red);
                }
            }

            if (layout == null)
            {
                layout = name switch
                {
                    _ => DefaultLayout.GetDefaultLayout(this)
                };
                OpenFiles.Clear();
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
            
            Serializer.Save(stream, Layout);
        }

        public void InitializeContent()
        {
            var extendedDocs = SearchView<IWaitForContent>();
            foreach (var extendedDocument in extendedDocs)
            {
                extendedDocument.InitializeContent();
            }
        }

        #endregion
    }
}