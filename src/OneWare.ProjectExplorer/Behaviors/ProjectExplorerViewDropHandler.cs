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
    private static string[] GetLocalPaths(DragEventArgs e)
    {
        return e.DataTransfer
                   .TryGetFiles()?
                   .Select(f => f.TryGetLocalPath())
                   .Where(p => !string.IsNullOrWhiteSpace(p))
                   .Cast<string>()
                   .ToArray()
               ?? Array.Empty<string>();
    }
 
    private static bool IsSameFolderMove(
        IProjectFolder targetFolder,
        IEnumerable<string> sourcePaths)
    {
        var targetPath = Path.GetFullPath(targetFolder.FullPath);

        return sourcePaths.Any(src =>
        {
            var sourceDir = Path.GetDirectoryName(Path.GetFullPath(src));
            return sourceDir != null &&
                   Path.Equals(sourceDir, targetPath);
        });
    }
    
    private bool Validate<T>(
        TreeDataGrid treeView,
        DragEventArgs e,
        object? sourceContext,
        object? targetContext,
        bool execute)
        where T : IProjectExplorerNode
    {
        if (targetContext is not ProjectExplorerViewModel vm)
            return false;

        if (treeView.GetVisualAt(e.GetPosition(treeView)) is not Control { DataContext: T targetNode })
            return false;

        var targetFolder =
            targetNode as IProjectFolder ??
            targetNode.Parent as IProjectFolder;

        if (targetFolder == null)
            return false;

        var sourcePaths = GetLocalPaths(e);
        if (sourcePaths.Length == 0)
            return false;
        
        if (e.DragEffects == DragDropEffects.Move &&
            IsSameFolderMove(targetFolder, sourcePaths))
            return false;

        if (!execute)
            return true;

        switch (e.DragEffects)
        {
            case DragDropEffects.Copy:
                _ = vm.DropAsync(targetFolder, true, true, sourcePaths);
                return true;

            case DragDropEffects.Move:
                _ = vm.DropAsync(targetFolder, true, false, sourcePaths);
                return true;

            default:
                return false;
        }
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