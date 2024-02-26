using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.Essentials.Models;

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
        if (sourceContext is not ICollection<T> sourceNodes)
        {
            if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
            {
                if (bExecute)
                {
                    _ = vm.DropAsync(targetParent, false, true, files
                        .Select(x => x.TryGetLocalPath())
                        .Where(x => x != null)
                        .Cast<string>()
                        .ToArray());
                }
                return true;
            }
            return false;
        }
        
        foreach (var sourceNode in sourceNodes)
        {
            if (targetParent == sourceNode.TopFolder) return false;
                
            if (sourceNode.FullPath == targetParent.FullPath) 
                return false;

            if (sourceNode is IProjectFolder && targetParent.FullPath.StartsWith(sourceNode.FullPath))
                return false;
        }

        switch (e.DragEffects)
        {
            case DragDropEffects.Copy:
            {
                if (bExecute)
                {
                    _ = vm.DropAsync(targetParent, true, true, sourceNodes.Select(x => x.FullPath).ToArray());
                }

                return true;
            }
            case DragDropEffects.Move:
            {
                if (bExecute)
                {
                    _ = vm.DropAsync(targetParent, true, false,sourceNodes.Select(x => x.FullPath).ToArray());
                }

                return true;
            }
            case DragDropEffects.Link:
            {
                return false;
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