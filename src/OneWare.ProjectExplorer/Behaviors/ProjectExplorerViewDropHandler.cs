using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using OneWare.Essentials.Models;
using OneWare.ProjectExplorer.ViewModels;

namespace OneWare.ProjectExplorer.Behaviors;

public class ProjectExplorerViewDropHandler : DropHandlerBase
{
    private bool Validate<T>(TreeDataGrid treeView, DragEventArgs e, object? sourceContext, object? targetContext,
        bool bExecute) where T : IProjectExplorerNode
    {
        if (targetContext is not ProjectExplorerViewModel vm
            || treeView.GetVisualAt(e.GetPosition(treeView)) is not Control { DataContext: T targetNode })
            return false;
        
        var targetParent = targetNode as IProjectFolder ?? targetNode.Parent as IProjectFolder;

        if (targetParent == null) return false;

        //Import files or folders from outside
        if (sourceContext is not IReadOnlyList<T> sourceNodes)
        {
            if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
            {
                if (bExecute)
                    _ = vm.DropAsync(targetParent, false, true, files
                        .Select(x => x.TryGetLocalPath())
                        .Where(x => x != null)
                        .Cast<string>()
                        .ToArray());
                return true;
            }

            return false;
        }

        if (!sourceNodes.All(x => x is IProjectEntry)) return false;

        var sourceEntities = sourceNodes.Cast<IProjectEntry>().ToArray();
        
        foreach (var sourceNode in sourceEntities)
        {
            if (targetParent == sourceNode.Parent) return false;

            if (sourceNode.FullPath == targetParent.FullPath)
                return false;

            if (sourceNode is IProjectFolder sourceFolder && targetParent.FullPath.StartsWith(sourceFolder.FullPath))
                return false;
        }
        
        switch (e.DragEffects)
        {
            case DragDropEffects.Copy:
            {
                if (bExecute)
                    _ = vm.DropAsync(targetParent, true, true, sourceEntities.Select(x => x.FullPath).ToArray());

                return true;
            }
            case DragDropEffects.Move:
            {
                if (bExecute)
                    _ = vm.DropAsync(targetParent, true, false, sourceEntities.Select(x => x.FullPath).ToArray());

                return true;
            }
            case DragDropEffects.Link:
            {
                return false;
            }
        }

        return false;
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Source is Control && sender is TreeDataGrid treeView)
        {
            var status = Validate<IProjectExplorerNode>(treeView, e, sourceContext, targetContext, false);
            return status;
        }
        
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Source is Control && sender is TreeDataGrid treeView)
            return Validate<IProjectExplorerNode>(treeView, e, sourceContext, targetContext, true);
        return true;
    }
}