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
               ?? [];
    }

    private static bool IsSameFolderMove(
        IProjectFolder targetFolder,
        IEnumerable<string> sourcePaths)
    {
        static string Normalize(string path)
        {
            var full = Path.GetFullPath(path);

            // normalize directory paths
            if (!full.EndsWith(Path.DirectorySeparatorChar))
                full += Path.DirectorySeparatorChar;

            return full;
        }

        var targetPath = Normalize(targetFolder.FullPath);

        return sourcePaths.Any(src =>
        {
            var fullSrc = Path.GetFullPath(src);

            if (File.Exists(fullSrc))
            {
                var sourceDir = Path.GetDirectoryName(fullSrc);
                return sourceDir != null &&
                       Normalize(sourceDir) == targetPath;
            }

            if (Directory.Exists(fullSrc))
            {
                var parentDir = Path.GetDirectoryName(
                    fullSrc.TrimEnd(Path.DirectorySeparatorChar));

                return parentDir != null &&
                       Normalize(parentDir) == targetPath;
            }

            return false;
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

            // Happens when a file is dragged from outside
            case DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link:
                _ = vm.DropAsync(targetFolder, false, true, sourcePaths);
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