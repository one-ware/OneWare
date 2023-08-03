using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.Shared;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.Behaviours;

public class ProjectExplorerViewDropHandler : DropHandlerBase
{
    private bool Validate<T>(TreeView treeView, DragEventArgs e, object? sourceContext, object? targetContext, bool bExecute) where T : IProjectEntry
    {
        if (targetContext is not ProjectExplorerViewModel vm
            || treeView.GetVisualAt(e.GetPosition(treeView)) is not Control { DataContext: T targetNode })
        {
            return false;
        }

        var targetParent = targetNode as IProjectFolder ?? targetNode.TopFolder;

        if (targetParent == null) return false;
        
        //Import files or folders from outside
        if (sourceContext is not T sourceNode)
        {
            if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
            {
                if (bExecute)
                {
                    _ = vm.ImportAsync(targetParent, true, true, files
                        .Select(x => x.TryGetLocalPath())
                        .Where(x => x != null)
                        .Cast<string>()
                        .ToArray());
                }
                return true;
            }
            return false;
        }
        
        var sourceParent = sourceNode.TopFolder;

        if (sourceParent is null) return false;
        
        var sourceNodes = sourceParent.Items;
        var targetNodes = targetParent.Items;

        if (sourceNodes != targetNodes)
        {
            if (sourceNode.FullPath == targetParent.FullPath) 
                return false;
            
            switch (e.DragEffects)
            {
                case DragDropEffects.Copy:
                {
                    if (bExecute)
                    {
                        _ = vm.ImportAsync(targetParent, true, true, sourceNode.FullPath);
                    }

                    return true;
                }
                case DragDropEffects.Move:
                {
                    if (bExecute)
                    {
                        _ = vm.ImportAsync(targetParent, false, true, sourceNode.FullPath);
                    }

                    return true;
                }
                case DragDropEffects.Link:
                {
                    return false;
                }
            }
        }

        return false;
    }
        
    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is TreeView treeView)
        {
            return Validate<IProjectEntry>(treeView, e, sourceContext, targetContext, false);
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is TreeView treeView)
        {
            return Validate<IProjectEntry>(treeView, e, sourceContext, targetContext, true);
        }
        return false;
    }
}