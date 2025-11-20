using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OneWare.ProjectExplorer;

public class TreeDataGridCustom : TreeDataGrid
{
    protected override Type StyleKeyOverride => typeof(TreeDataGrid);

    static TreeDataGridCustom()
    {
        //Hack fix to disable the dragdrop logic from original TreeDataGrid since we use our own behavior
        DragDrop.DragOverEvent.AddClassHandler<TreeDataGridCustom>((x, e) => e.Handled = true);
        DragDrop.DragLeaveEvent.AddClassHandler<TreeDataGridCustom>((x, e) => e.Handled = true);
    }
}