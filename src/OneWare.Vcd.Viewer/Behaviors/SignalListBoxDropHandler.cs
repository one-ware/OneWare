using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Viewer.ViewModels;

namespace OneWare.Vcd.Viewer.Behaviors;

public class SignalListBoxDropHandler : DropHandlerBase
{
    private bool Validate<T>(ContentControl viewer, DragEventArgs e, object? sourceContext, object? targetContext,
        bool bExecute) where T : IVcdSignal
    {
        if (sourceContext is not T sourceItem || targetContext is not VcdViewModel vm) return false;

        if (bExecute) vm.AddSignal(sourceItem);
        return true;
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Source is Control && sender is ContentControl viewer)
            return Validate<IVcdSignal>(viewer, e, sourceContext, targetContext, false);
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Source is Control && sender is ContentControl viewer)
            return Validate<IVcdSignal>(viewer, e, sourceContext, targetContext, true);
        return false;
    }
}