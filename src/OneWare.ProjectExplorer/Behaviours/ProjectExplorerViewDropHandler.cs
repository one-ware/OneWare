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
                    vm.ImportStorageItems(targetParent, files.ToArray());
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
            // var sourceIndex = sourceNodes.IndexOf(sourceNode);
            // var targetIndex = targetNodes.IndexOf(targetNode);
            //
            // if (sourceIndex < 0 || targetIndex < 0)
            // {
            //     return false;
            // }

            switch (e.DragEffects)
            {
                case DragDropEffects.Copy:
                {
                    if (bExecute)
                    {
                        try
                        {
                            switch (sourceNode)
                            {
                                case IProjectFile:
                                    Tools.CopyFile(sourceNode.FullPath, Path.Combine(targetParent.FullPath, sourceNode.Header));
                                    break;
                                case IProjectFolder:
                                    Tools.CopyDirectory(sourceNode.FullPath, Path.Combine(targetParent.FullPath, sourceNode.Header));
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            ContainerLocator.Container.Resolve<ILogger>().Error(ex.Message, ex);
                        }
                    }

                    return true;
                }
                case DragDropEffects.Move:
                {
                    if (bExecute)
                    {
                        try
                        {
                            switch (sourceNode)
                            {
                                case IProjectFile:
                                    File.Move(sourceNode.FullPath, Path.Combine(targetParent.FullPath, sourceNode.Header));
                                    break;
                                case IProjectFolder:
                                    Directory.Move(sourceNode.FullPath, Path.Combine(targetParent.FullPath, sourceNode.Header));
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            ContainerLocator.Container.Resolve<ILogger>().Error(ex.Message, ex);
                        }
                    }

                    return true;
                }
                case DragDropEffects.Link:
                {
                    if (bExecute)
                    {
                        
                    }

                    return true;
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