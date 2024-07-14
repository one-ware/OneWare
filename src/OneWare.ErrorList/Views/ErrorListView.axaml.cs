using Avalonia.Controls;
using Avalonia.Interactivity;
using OneWare.ErrorList.ViewModels;

namespace OneWare.ErrorList.Views;

public partial class ErrorListView : UserControl
{
    public ErrorListView()
    {
        InitializeComponent();

        SearchErrors.Search += (o, i) => (DataContext as ErrorListViewModel)?.Filter();

        ErrorList.AddHandler(DoubleTappedEvent, Open_Error, RoutingStrategies.Bubble);
    }

    private void Open_Error(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ErrorListViewModel el) _ = el.GoToErrorAsync();
    }
}