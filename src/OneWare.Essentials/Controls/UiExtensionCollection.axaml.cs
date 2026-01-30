using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Controls;

public class UiExtensionCollection : TemplatedControl
{
    private static readonly FuncTemplate<Panel?> DefaultPanel =
        new(() => new StackPanel { Orientation = Orientation.Horizontal });

    public static readonly StyledProperty<object?> ContextProperty =
        AvaloniaProperty.Register<UiExtensionControl, object?>(nameof(Context));

    public static readonly StyledProperty<ObservableCollection<OneWareUiExtension>?> ExtensionsProperty =
        AvaloniaProperty.Register<UiExtensionCollection, ObservableCollection<OneWareUiExtension>?>(nameof(Extensions));

    public static readonly StyledProperty<ITemplate<Panel?>> ItemsPanelProperty =
        AvaloniaProperty.Register<UiExtensionCollection, ITemplate<Panel?>>(nameof(ItemsPanel), DefaultPanel);

    public object? Context
    {
        get => GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    public ObservableCollection<OneWareUiExtension>? Extensions
    {
        get => GetValue(ExtensionsProperty);
        set => SetValue(ExtensionsProperty, value);
    }

    /// <summary>
    ///     Gets or sets the panel used to display the items.
    /// </summary>
    public ITemplate<Panel?> ItemsPanel
    {
        get => GetValue(ItemsPanelProperty);
        set => SetValue(ItemsPanelProperty, value);
    }
}