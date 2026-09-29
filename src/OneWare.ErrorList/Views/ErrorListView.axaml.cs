using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OneWare.ErrorList.ViewModels;

namespace OneWare.ErrorList.Views;

public partial class ErrorListView : UserControl
{
    private const double MinSearchWidth = 200;

    public ErrorListView()
    {
        InitializeComponent();

        ProblemTree.AddHandler(DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Bubble);
        ProblemTree.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        HeaderGrid.SizeChanged += (_, _) => UpdateHeaderLayout();
    }

    private void UpdateHeaderLayout()
    {
        var wrap = HeaderGrid.Bounds.Width < FilterToolbar.DesiredSize.Width + MinSearchWidth;

        Grid.SetRow(FilterToolbar, wrap ? 1 : 0);
        Grid.SetColumn(FilterToolbar, wrap ? 0 : 1);
        Grid.SetColumnSpan(FilterToolbar, wrap ? 2 : 1);
        FilterToolbar.Margin = wrap ? new Thickness(0, 4, 0, 0) : new Thickness(6, 0, 0, 0);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double clicking the expander chevron should only toggle the group
        if (e.Source is Visual visual && visual.FindAncestorOfType<ToggleButton>(true) != null) return;
        if (DataContext is ErrorListViewModel vm) _ = vm.GoToErrorAsync();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ErrorListViewModel vm) return;
        _ = vm.GoToErrorAsync();
        e.Handled = true;
    }
}
