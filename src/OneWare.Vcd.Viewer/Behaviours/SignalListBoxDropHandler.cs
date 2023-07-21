using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using OneWare.Core.ViewModels.Windows;
using OneWare.Vcd.Viewer.ViewModels;
using OneWare.Vcd.Viewer.Models;

namespace OneWare.Vcd.Viewer.Behaviours;

public class SignalListBoxDropHandler : DropHandlerBase
{
    private bool Validate<T>(ContentControl viewer, DragEventArgs e, object? sourceContext, object? targetContext, bool bExecute) where T : VcdSignal
    {
        if (sourceContext is not T sourceItem || targetContext is not VcdViewModel vm)
        {
            return false;
        }

        if (bExecute)
        {
            vm.AddSignal(sourceItem);
        }
        return true;
    }
        
    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is ContentControl viewer)
        {
            return Validate<VcdSignal>(viewer, e, sourceContext, targetContext, false);
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is ContentControl viewer)
        {
            return Validate<VcdSignal>(viewer, e, sourceContext, targetContext, true);
        }
        return false;
    }
}