using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Extensions;
using OneWare.Shared;
using OneWare.Shared.Models;

namespace OneWare.Core.Views.Windows
{
    public partial class MainWindow : AdvancedWindow
    {
        public INotificationManager NotificationManager { get; }

        private NativeMenu? _nativeMenu;
        
        public MainWindow()
        {
            InitializeComponent();
            
#if DEBUG
            this.AttachDevTools();
#endif

            NotificationManager = new WindowNotificationManager(this)
            {
                Position = NotificationPosition.TopRight,
                Margin = new Thickness(0, 55, 5, 0),
                BorderThickness = new Thickness(0),
            };

            this.WhenValueChanged(x => x.DataContext).Subscribe(x =>
            {
                var vm = DataContext as MainWindowViewModel;
                if (vm == null) return;
                
                // this.AddHandler(PointerPressedEvent, (o, i) =>
                // {
                //     if (!ViewModel.IsStatusLoading) ViewModel.StatusText = "Ready";
                // }, RoutingStrategies.Bubble, true).DisposeWith(disposableRegistration);

                vm.MainMenu.ObserveCollectionChanges()
                    .Throttle(new TimeSpan(100))
                    .Subscribe(_ =>
                    {
                        Dispatcher.UIThread.Post(RefreshNativeMenu);
                    });
            });
            
            /*AddHandler(KeyDownEvent, (o, i) =>
            {
                //Doc switching hotkeys TODO Allow in hostwindows aswell
                if (dockService.DocumentDock.VisibleDockables is { } docs)
                {
                    if (i.KeyModifiers == KeyModifiers.Control && i.Key == Key.Tab)
                    {
                        if (docs.Count > 1)
                        {
                            var currentIndex = docs.IndexOf(DockService.DocumentDock.ActiveDockable);
                            DockService.DocumentDock.ActiveDockable =
                                docs[currentIndex + 1 < docs.Count ? currentIndex + 1 : 0];
                            i.Handled = true;
                        }
                    }

                    else if (i.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && i.Key == Key.Tab)
                    {
                        if (docs.Count > 1)
                        {
                            var currentIndex = docs.IndexOf(DockService.DocumentDock.ActiveDockable);
                            DockService.DocumentDock.ActiveDockable =
                                docs[currentIndex - 1 >= 0 ? currentIndex - 1 : docs.Count - 1];
                            i.Handled = true;
                        }
                    }
                }

                if (i.Key == Key.F && i.KeyModifiers.HasFlag(Global.ControlKey) &&
                    i.KeyModifiers.HasFlag(KeyModifiers.Shift)) Global.Factory.ShowDockable(DockService.SearchList);
            }, RoutingStrategies.Bubble, true);


            //Nativemenu
            var mainMenu = this.Find<Menu>("MainMenu");
            var nm = new NativeMenu();

            ConvertMenuToNativeMenu(mainMenu, nm);

            NativeMenu.SetMenu(this, nm);
            */
        }

        private void RefreshNativeMenu()
        {
            if (DataContext is not MainWindowViewModel vm) return;
            _nativeMenu = new NativeMenu();
            _nativeMenu.Items.Clear();
            ConvertMenuToNativeMenu(vm.MainMenu, _nativeMenu);
            NativeMenu.SetMenu(this, _nativeMenu);
                
            if (NativeMenu.GetIsNativeMenuExported(this) && TitleBarContent != null)
            {
                TitleBarContent.IsVisible = false;
            }
            else
            {
                ShowTitle = false;
            }
        }
        private void ConvertMenuToNativeMenu(IEnumerable<object> m, NativeMenu nm)
        {
            foreach (var item in m)
                if (item is MenuItemViewModel mi)
                {
                    var nmi = new NativeMenuItem(mi.Header as string ?? "")
                    {
                        Gesture = mi.Hotkey
                    };

                    if (mi.Icon is CheckBox cb)
                    {
                        nmi.ToggleType = NativeMenuItemToggleType.CheckBox;
                        var obsvr = Observer.Create<bool?>(
                            x => nmi.IsChecked = x ?? false,
                            ex => { },
                            () => { });

                        nmi.IsChecked = true;

                        cb.GetObservable(ToggleButton.IsCheckedProperty).Subscribe(obsvr);
                    }
                    else if (mi.Icon is Image { Source: Bitmap btm })
                    {
                        nmi.Icon = btm;
                    }

                    nmi.Bind(NativeMenuItem.CommandProperty, mi.WhenValueChanged(x => x.Command));
                    nmi.Bind(NativeMenuItem.IsEnabledProperty, mi.WhenValueChanged(x => x.IsEnabled));
                    nmi.Bind(NativeMenuItem.CommandParameterProperty, mi.WhenValueChanged(x => x.CommandParameter));

                    nm.Add(nmi);
                    if (mi.Items != null && !mi.Items.IsNullOrEmpty())
                    {
                        nmi.Menu = new NativeMenu();
                        ConvertMenuToNativeMenu(mi.Items, nmi.Menu);
                    }
                }
                else if (item is Separator s)
                {
                    nm.Add(new NativeMenuItemSeparator());
                }
        }
    }
}