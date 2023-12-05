using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OneWare.ApplicationCommands.ViewModels;
using OneWare.SDK.Controls;

namespace OneWare.ApplicationCommands.Views;

public partial class CommandManagerView : FlexibleWindow
{
    public CommandManagerView()
    {
        InitializeComponent();
        
        AddHandler(PointerReleasedEvent, (sender, args) =>
        {
            if (args.Source is ILogical logical)
            {
                var parent = logical.FindLogicalAncestorOfType<ListBoxItem>();
                if (parent != null)
                {
                    (DataContext as CommandManagerViewModel)?.ExecuteSelection(this);
                }
            }
        }, RoutingStrategies.Bubble, true);
    }
    
    protected override void AttachedToHost()
    {
        base.AttachedToHost();
        if (Host is { Owner: Window ownerWindow })
        {
            Host.Position = new PixelPoint(Host.Position.X, ownerWindow.Position.Y + 50);
        }
    }
}