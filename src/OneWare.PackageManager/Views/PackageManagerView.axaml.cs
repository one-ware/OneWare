using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Services;
using OneWare.PackageManager.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;

namespace OneWare.PackageManager.Views;

public partial class PackageManagerView : FlexibleWindow
{
    private PackageManagerViewModel? _viewModel;
    private Vector _browseOffset;
    private bool _showingDetails;

    public PackageManagerView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ObserveViewModel();
        AttachedToVisualTree += (_, _) => ObserveViewModel();
        DetachedFromVisualTree += (_, _) =>
        {
            if (_viewModel != null) _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            _viewModel = null;
        };
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.F && args.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (DataContext is PackageManagerViewModel vm) vm.SelectedPackage = null;
                Dispatcher.UIThread.Post(() => PackageSearch.Focus(), DispatcherPriority.Loaded);
                args.Handled = true;
            }
            else if (args.Key == Key.Left && args.KeyModifiers.HasFlag(KeyModifiers.Alt) && DataContext is PackageManagerViewModel { IsDetails: true } vm)
            {
                vm.BackCommand.Execute(null);
                args.Handled = true;
            }
        };
    }

    private void ObserveViewModel()
    {
        if (_viewModel != null) _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel = DataContext as PackageManagerViewModel;
        _showingDetails = _viewModel?.IsDetails == true;
        if (_viewModel != null) _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(PackageManagerViewModel.IsDetails) || _viewModel == null) return;
        var details = _viewModel.IsDetails;
        if (details == _showingDetails) return;
        _showingDetails = details;
        var scroll = PluginList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (details)
        {
            _browseOffset = scroll?.Offset ?? default;
            Dispatcher.UIThread.Post(() => BackButton.Focus(), DispatcherPriority.Loaded);
        }
        else
            Dispatcher.UIThread.Post(() =>
            {
                if (_viewModel?.IsDetails != false) return;
                PluginList.Focus();
                if (scroll != null) scroll.Offset = _browseOffset;
            }, DispatcherPriority.Loaded);
    }

    private void Details_OnClick(object? sender, RoutedEventArgs args)
    {
        if (DataContext is PackageManagerViewModel vm && sender is Control { DataContext: PackageViewModel package })
            vm.SelectedPackage = package;
    }

    private void PluginList_OnDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (args.Source is Control source && (source is Button || source.GetVisualAncestors().OfType<Button>().Any())) return;
        OpenSelectedPackage();
    }

    private void PluginList_OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || args.Source is Button) return;
        OpenSelectedPackage();
        args.Handled = true;
    }

    private void OpenSelectedPackage()
    {
        if (DataContext is PackageManagerViewModel vm && PluginList.SelectedItem is PackageViewModel package)
            vm.SelectedPackage = package;
    }

    private void Sources_OnClick(object? sender, RoutedEventArgs args)
    {
        var container = ContainerLocator.Container;
        var settings = new ApplicationSettingsViewModel(container.Resolve<ISettingsService>(), container.Resolve<IPaths>(), container.Resolve<IWindowService>());
        settings.SelectedPage = settings.SettingPages.FirstOrDefault(x => x.Header == "Package Manager");
        _ = container.Resolve<IWindowService>().ShowDialogAsync(new ApplicationSettingsView { DataContext = settings }, Host);
    }
}