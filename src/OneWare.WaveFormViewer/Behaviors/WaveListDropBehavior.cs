using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using OneWare.WaveFormViewer.Models;
using OneWare.WaveFormViewer.ViewModels;

namespace OneWare.WaveFormViewer.Behaviors;

public class WaveListDropBehavior : DropHandlerBase
{
    public override void Enter(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        base.Enter(sender, e, sourceContext, targetContext);
        if (e.DragEffects == DragDropEffects.None) e.Handled = false;
    }

    public override void Over(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        base.Over(sender, e, sourceContext, targetContext);
        if (e.DragEffects == DragDropEffects.None) e.Handled = false;
    }

    public override void Drop(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        base.Drop(sender, e, sourceContext, targetContext);
        if (e.DragEffects == DragDropEffects.None) e.Handled = false;
    }

    private bool Validate<T>(ListBox listBox, DragEventArgs e, object? sourceContext, object? targetContext,
        bool bExecute) where T : WaveModel
    {
        if (sourceContext is not T sourceItem
            || targetContext is not WaveFormViewModel vm
            || listBox.GetVisualAt(e.GetPosition(listBox)) is not Control targetControl
            || targetControl.DataContext is not T targetItem)
            return false;

        var items = vm.Signals;
        var sourceIndex = items.IndexOf(sourceItem);
        var targetIndex = items.IndexOf(targetItem);

        if (sourceIndex < 0 || targetIndex < 0) return false;

        switch (e.DragEffects)
        {
            case DragDropEffects.Copy:
            {
                return false;
            }
            case DragDropEffects.Move:
            {
                if (bExecute) MoveItem(items, sourceIndex, targetIndex);
                return true;
            }
            case DragDropEffects.Link:
            {
                if (bExecute) SwapItem(items, sourceIndex, targetIndex);
                return true;
            }
            default:
                return false;
        }
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Source is Control && sender is ListBox listBox)
            return Validate<WaveModel>(listBox, e, sourceContext, targetContext, false);
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Source is Control && sender is ListBox listBox)
            return Validate<WaveModel>(listBox, e, sourceContext, targetContext, true);
        return false;
    }
}