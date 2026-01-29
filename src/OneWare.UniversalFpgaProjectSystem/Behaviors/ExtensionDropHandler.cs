using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Behaviors;

public class ExtensionDropHandler : DropHandlerBase
{
    private static bool HandleDrop(Control control, DragEventArgs e, ExtensionModel sourceContext,
        HardwareInterfaceModel targetContext,
        bool bExecute)
    {
        if (sourceContext.FpgaExtension.Connector != targetContext.Interface.Connector ||
            sourceContext.ParentInterfaceModel == null || sourceContext.ParentInterfaceModel == targetContext)
            return false;


        var parent = targetContext.Owner;

        while (parent != null)
        {
            parent = (parent as ExtensionModel)?.ParentInterfaceModel?.Owner;
            if (parent == sourceContext) return false;
        }

        if (bExecute) targetContext.DropExtension(sourceContext.ParentInterfaceModel);

        return true;
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Source is Control && sender is Control control && sourceContext is ExtensionModel source &&
            targetContext is HardwareInterfaceModel target)
            return HandleDrop(control, e, source, target, false);
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Source is Control && sender is Control control && sourceContext is ExtensionModel source &&
            targetContext is HardwareInterfaceModel target)
            return HandleDrop(control, e, source, target, true);
        return false;
    }
}