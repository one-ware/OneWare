using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
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
        
        if (sourceContext is not T sourceNode)
        {
            //Import Files from Explorer
            if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
            {
                if (bExecute)
                {
                    foreach (var f in files)
                    {
                        var path = f.TryGetLocalPath();
                        if (path == null) continue;
                        
                        var attr = File.GetAttributes(path);

                        if (attr.HasFlag(FileAttributes.Directory))
                        {
                            var folder = targetParent.AddFolder(Path.GetFileName(path));
                            vm.ImportFolderRecursive(f.Path.LocalPath, folder);
                        }
                        else
                            vm.ImportFile(f.Path.LocalPath, targetParent);
                    }
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
                        //var clone = new NodeViewModel() { Title = sourceNode.Title + "_copy" };
                        //InsertItem(targetNodes, clone, targetIndex + 1);
                    }

                    return true;
                }
                case DragDropEffects.Move:
                {
                    if (bExecute)
                    {
                        if (sourceNodes == targetNodes)
                        {
                            //MoveItem(sourceNodes, sourceIndex, targetIndex);
                        }
                        else
                        {
                            try
                            {
                                sourceParent.Remove(sourceNode);
                                if (sourceNode is IProjectFile)
                                {
                                    File.Move(sourceNode.FullPath, Path.Combine(targetParent.FullPath, sourceNode.Header));
                                    targetParent.AddFile(sourceNode.Header);
                                }
                                else if (sourceNode is IProjectFolder)
                                {
                                    
                                }
                            }
                            catch (Exception ex)
                            {
                                ContainerLocator.Container.Resolve<ILogger>().Error(ex.Message, ex);
                            }
                        }
                    }

                    return true;
                }
                case DragDropEffects.Link:
                {
                    if (bExecute)
                    {
                        if (sourceNodes == targetNodes)
                        {
                            //SwapItem(sourceNodes, sourceIndex, targetIndex);
                        }
                        else
                        {
                            //sourceNode.Parent = targetParent;
                            //targetNode.Parent = sourceParent;

                            //SwapItem(sourceNodes, targetNodes, sourceIndex, targetIndex);
                        }
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