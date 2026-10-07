using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Core.Extensions;
using OneWare.Core.ViewModels.Windows;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.Views.Windows;

public partial class MainWindow : AdvancedWindow
{
    private IDisposable? _menuTreeSubscription;
    private NativeMenu? _nativeMenu;

    public MainWindow()
    {
        InitializeComponent();

        this.WhenValueChanged(x => x.DataContext).Subscribe(x =>
        {
            if (DataContext is not MainWindowViewModel vm) return;

            // this.AddHandler(PointerPressedEvent, (o, i) =>
            // {
            //     if (!ViewModel.IsStatusLoading) ViewModel.StatusText = "Ready";
            // }, RoutingStrategies.Bubble, true).DisposeWith(disposableRegistration);

            // Watch the whole tree so items registered in submenus (e.g. View > Tool Windows) also show up
            _menuTreeSubscription?.Dispose();
            var menuChanged = new Subject<Unit>();
            var watchTree = vm.MainMenu.WatchTreeChanges((_, _) => menuChanged.OnNext(Unit.Default),
                (_, _) => menuChanged.OnNext(Unit.Default));
            // Subscribed after WatchTreeChanges so the initial pass over existing items doesn't trigger a refresh
            _menuTreeSubscription = new CompositeDisposable(watchTree,
                menuChanged
                    .Throttle(TimeSpan.FromMilliseconds(10))
                    .Subscribe(_ => { Dispatcher.UIThread.Post(RefreshNativeMenu); }));
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

    public WindowNotificationManager? NotificationManager { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                //ExtendClientAreaTitleBarHeightHint = WindowState is WindowState.Maximized or WindowState.FullScreen ? 37 : 30;
                BottomStatusRow.CornerRadius = WindowState == WindowState.Maximized
                    ? new CornerRadius(0)
                    : PlatformHelper.WindowsCornerRadiusBottom;
    }

    private void RefreshNativeMenu()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        _nativeMenu ??= new NativeMenu();
        _nativeMenu.Items.Clear();
        ConvertMenuToNativeMenu(vm.MainMenu, _nativeMenu);
        NativeMenu.SetMenu(this, _nativeMenu);

        if (NativeMenu.GetIsNativeMenuExported(this) && TitleBarContent != null)
            TitleBarContent.IsVisible = false;
        else
            ShowTitle = false;
    }

    private void ConvertMenuToNativeMenu(IEnumerable<object> m, NativeMenu nm)
    {
        foreach (var item in m)
            if (item is MenuItemModel mi)
            {
                var nmi = new NativeMenuItem();

                nmi.Bind(NativeMenuItem.HeaderProperty, mi.WhenValueChanged(x => x.Header).Select(x => x ?? ""));
                nmi.Bind(NativeMenuItem.GestureProperty, mi.WhenValueChanged(x => x.InputGesture));

                if (mi.Icon?.IconObservable is IObservable<Bitmap> btm) nmi.Bind(NativeMenuItem.IconProperty, btm);

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