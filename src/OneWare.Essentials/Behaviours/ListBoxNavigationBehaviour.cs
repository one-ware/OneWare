using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace OneWare.Essentials.Behaviours;

public class ListBoxNavigationBehaviour : Behavior<Control>
{
    public static readonly StyledProperty<ListBox?> AssociatedListBoxProperty =
        AvaloniaProperty.Register<ListBoxNavigationBehaviour, ListBox?>(nameof(AssociatedListBox));

    public ListBox? AssociatedListBox
    {
        get => GetValue(AssociatedListBoxProperty);
        set => SetValue(AssociatedListBoxProperty, value);
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
                if (AssociatedListBox == null) return;
                
                if (e.Key == Key.Down)
                {
                    if(AssociatedListBox.SelectedIndex < AssociatedListBox.Items.Count - 1) AssociatedListBox.SelectedIndex++;
                }
                else if (e.Key == Key.Up)
                {
                    if(AssociatedListBox.SelectedIndex > 0) AssociatedListBox.SelectedIndex--;
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