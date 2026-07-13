using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OneWare.Essentials.Controls;
using OneWare.PackageManager.ViewModels;

namespace OneWare.PackageManager.Views;

public partial class PackageManagerView : FlexibleWindow
{
    private PackageViewModel? _lastSelectedPackage;
    private ScrollViewer? _pluginListScrollViewer;

    public PackageManagerView()
    {
        InitializeComponent();

        PluginList.SelectionChanged += PluginList_OnSelectionChanged;
    }

    private void PackageSeparatorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (DataContext is not PackageManagerViewModel viewModel)
            return;

        var separators = viewModel.SelectedCategory?.VisibleSeparators;
        if (separators == null || separators.Count == 0)
            return;

        var flyout = new MenuFlyout();

        foreach (var separator in separators)
        {
            var menuItem = new MenuItem
            {
                Header = separator.Text
            };

            menuItem.Click += (_, _) => ScrollToSeparator(separator);
            flyout.Items.Add(menuItem);
        }

        flyout.ShowAt(button);
    }

    private void PluginList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PluginList.SelectedItem is PackageViewModel packageViewModel)
        {
            _lastSelectedPackage = packageViewModel;
            return;
        }

        if (PluginList.SelectedItem is not PackageSeparatorViewModel)
            return;

        Dispatcher.UIThread.Post(() => PluginList.SelectedItem = _lastSelectedPackage, DispatcherPriority.Input);
    }

    private void ScrollToSeparator(PackageSeparatorViewModel separator)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ScrollSeparatorContainerToTop(separator);
        }, DispatcherPriority.Background);
    }

    private void ScrollSeparatorContainerToTop(PackageSeparatorViewModel separator)
    {
        var separatorContainer = PluginList.ContainerFromItem(separator) as Control;
        if (separatorContainer == null)
        {
            PluginList.ScrollIntoView(separator);
            separatorContainer = PluginList.ContainerFromItem(separator) as Control;
        }

        if (separatorContainer == null)
            return;

        var scrollViewer = GetPluginListScrollViewer();
        if (scrollViewer == null)
        {
            separatorContainer.BringIntoView();
            return;
        }

        var maxOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        var targetOffset = Math.Clamp(separatorContainer.Bounds.Top, 0, maxOffset);
        scrollViewer.Offset = scrollViewer.Offset.WithY(targetOffset);
    }

    private ScrollViewer? GetPluginListScrollViewer()
    {
        if (_pluginListScrollViewer != null)
            return _pluginListScrollViewer;

        _pluginListScrollViewer = PluginList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        return _pluginListScrollViewer;
    }
}