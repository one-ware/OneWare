using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace OneWare.Essentials.Behaviours;

public class DataGridNavigationBehaviour : Behavior<Control>
{
    public static readonly StyledProperty<DataGrid?> AssociatedDataGridProperty =
        AvaloniaProperty.Register<DataGridNavigationBehaviour, DataGrid?>(nameof(AssociatedDataGrid));

    public DataGrid? AssociatedDataGrid
    {
        get => GetValue(AssociatedDataGridProperty);
        set => SetValue(AssociatedDataGridProperty, value);
    }
    
    private CompositeDisposable? Disposables { get; set; }

    protected override void OnAttached()
    {
        if (AssociatedObject == null) return;
        Disposables = new CompositeDisposable();

        base.OnAttached();

        Disposables.Add(AssociatedObject.AddDisposableHandler(
            InputElement.KeyDownEvent,
            (sender, e) =>
            {
                if (AssociatedDataGrid == null) return;
                
                if (e.Key == Key.Down)
                {
                    if(AssociatedDataGrid.SelectedIndex < (AssociatedDataGrid.ItemsSource).Cast<object>().Count() - 1) AssociatedDataGrid.SelectedIndex++;
                }
                else if (e.Key == Key.Up)
                {
                    if(AssociatedDataGrid.SelectedIndex > 0) AssociatedDataGrid.SelectedIndex--;
                }
            },
            RoutingStrategies.Tunnel, false));
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        Disposables?.Dispose();
    }
}