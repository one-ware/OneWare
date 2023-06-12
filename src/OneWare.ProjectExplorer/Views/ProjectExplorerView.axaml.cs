using Avalonia.Controls;
using Avalonia.Interactivity;
using DynamicData.Binding;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.Shared.Views;

namespace OneWare.ProjectExplorer.Views;

public partial class ProjectExplorerView : UserControl
{
    public ProjectExplorerView()
    {
        InitializeComponent();

        this.WhenValueChanged(x => x.DataContext).Subscribe(x =>
        {
            var vm = x as ProjectExplorerViewModel;
            if (vm == null) return;
            
            AddHandler(SearchBox.SearchEvent, (o, i) =>
            {
                vm.OnSearch();
            }, RoutingStrategies.Bubble);
        });

        TreeViewContextMenu.Opening += (sender, args) =>
        {
            (DataContext as ProjectExplorerViewModel)?.ConstructContextMenu();
        };

        /*
        AddHandler(PointerPressedEvent, (o, i) =>
        {
            if (i.Handled) return;

            //USED FOR DRAG DROP, Capture start point
            if (i.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
                if (Tools.VisualUpwardSearch<TreeViewItem>(i.Source as Interactive) == null) return;

                if (!_capturedStartPoint)
                {
                    _startPoint = i.GetPosition(this);
                    _capturedStartPoint = true;
                }
            }
        }, RoutingStrategies.Bubble, true);

        AddHandler(PointerReleasedEvent, (o, e) =>
        {
            if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                _capturedStartPoint = false;
                _isDragging = false;
                _pointerPressed = false;
            }
        }, RoutingStrategies.Tunnel, true);

        AddHandler(DragDrop.DropEvent, Drag_Finished);
        AddHandler(PointerMovedEvent, Pointer_Moved);
        ProjectTree.AddHandler(PointerPressedEvent, Pointer_Pressed, RoutingStrategies.Tunnel, true);*/
    }
    
    /*

    #region DragDrop

    private Point _startPoint;
    private bool _capturedStartPoint;
    private bool _isDragging;
    private bool _pointerPressed;

    private void Pointer_Moved(object? sender, PointerEventArgs e)
    {
        if (!_capturedStartPoint || _isDragging || !_pointerPressed) return;

        //10 Pixels difference needed to trigger Dragdrop
        if (Math.Abs(e.GetPosition(ProjectTree).X - _startPoint.X) >
            10 ||
            Math.Abs(e.GetPosition(ProjectTree).Y - _startPoint.Y) >
            10)
            if (DataContext is ProjectViewModelBase vm && vm.EnableDragDrop)
                _ = Drag_StartAsync(e);
    }
        
    /// <summary>
    ///     Allows selection with rightclick / Deprecated in 0.1
    /// </summary>
    private void Pointer_Pressed(object? sender, PointerPressedEventArgs e)
    {
        //USED FOR DRAG DROP, Capture start point
        if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            _pointerPressed = true;

            if (!_capturedStartPoint)
            {
                _startPoint = e.GetPosition(ProjectTree);
                _capturedStartPoint = true;
            }
        }
    }

    private async Task Drag_StartAsync(PointerEventArgs e)
    {
        if (ProjectTree.SelectedItems.Count == 0) return;
            
        var data = new DataObject();

        if (Tools.VisualUpwardSearch<TreeViewItem>(e.Source as Interactive) is TreeViewItem tvi)
        {
            data.Set("inadt", ProjectTree.SelectedItems);

            var dde = DragDropEffects.Move;
            _isDragging = true;
            DragDrop.SetAllowDrop(ProjectTree, true);
            var de = await DragDrop.DoDragDrop(e, data, dde);

            //After drag complete
            _isDragging = false;
            _capturedStartPoint = false;
        }
    }

    /// <summary>
    ///     Finishes Tab DragDrop and swaps tabs
    /// </summary>
    private void Drag_Finished(object? sender, DragEventArgs e)
    {
        try
        {
            if (_isDragging && e.Data.Get("inadt") is IList<ProjectEntry> source)
            {
                var destinationitem = Tools.VisualUpwardSearch<TreeViewItem>(e.Source as Interactive);
                if (destinationitem != null)
                {
                    if (destinationitem.DataContext is ProjectFolder folder) //ITEM DROPPED ONTO FOLDER
                    {
                        foreach (var entry in source)
                            if (entry == destinationitem.DataContext)
                                return;
                            
                        _ = MainDock.ProjectFiles.MoveDialogAsync(folder, source.ToArray());
                    }
                    else //ITEM DROPPED ON FILE
                    {
                        var pfile = destinationitem.DataContext as ProjectFile;
                        if (pfile.TopFolder == null)
                        {
                            //DROP INTO SOLUTION NOT SUPPORTED
                            //MainDock.ProjectFiles.Move(source);
                        }
                        else
                        {
                            foreach (var entry in source)
                                if (entry == pfile.TopFolder)
                                {
                                    ContainerLocator.Container.Resolve<ILogger>()?.Error("Drag&Drop operation failed: Can't drop folder inside itself");
                                    return;
                                }

                            _ = MainDock.ProjectFiles.MoveDialogAsync(pfile.TopFolder, source.ToArray());
                        }
                    }
                }
                else
                {
                    _ = MainDock.ProjectFiles.MoveDialogAsync(MainDock.ProjectFiles.ProjectEntries[MainDock.ProjectFiles.ProjectEntries.Count - 1] as ProjectFolder, source.ToArray());
                }

                //Refresh selection
                //MainDock.ProjectFiles.SelectedItems.Clear();
                //foreach (var i in source) MainDock.ProjectFiles.SelectedItems.Add(i);
            }
            else if (e.Data.GetFileNames() is { } fileNames) //Drag drop from outside (eg explorer)
            {
                var destinationitem = Tools.VisualUpwardSearch<TreeViewItem>(e.Source as Interactive);
                if (destinationitem != null)
                {
                    if (destinationitem.DataContext is ProjectFolder folder) //ITEM DROPPED ONTO FOLDER
                    {
                        foreach (var file in fileNames)
                        {
                            var attr = File.GetAttributes(file);

                            //detect whether its a directory or file
                            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                                MainDock.ProjectFiles.ImportFolderRecursive(file,
                                    folder.AddFolder(Path.GetFileName(file)));
                            else MainDock.ProjectFiles.ImportFile(file, folder);
                        }
                    }
                    else //ITEM DROPPED ON FILE
                    {
                        var pfile = destinationitem.DataContext as ProjectFile;
                        if (pfile.TopFolder == null)
                        {
                            //DROP INTO SOLUTION NOT SUPPORTED
                            //MainDock.ProjectFiles.Move(source);
                        }
                        else
                        {
                            foreach (var file in fileNames)
                            {
                                var attr = File.GetAttributes(file);

                                //detect whether its a directory or file
                                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                                    MainDock.ProjectFiles.ImportFolderRecursive(file,
                                        pfile.TopFolder.AddFolder(Path.GetFileName(file)));
                                else MainDock.ProjectFiles.ImportFile(file, pfile.TopFolder);
                            }
                        }
                    }
                }
                else if (MainDock.ProjectFiles.ProjectEntries.Any())
                {
                    foreach (var file in fileNames)
                    {
                        var attr = File.GetAttributes(file);

                        //detect whether its a directory or file
                        if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                            MainDock.ProjectFiles.ImportFolderRecursive(file,
                                (MainDock.ProjectFiles.ProjectEntries[
                                    MainDock.ProjectFiles.ProjectEntries.Count - 1] as ProjectFolder)
                                ?.AddFolder(Path.GetFileName(file)));
                        else
                            MainDock.ProjectFiles.ImportFile(file,
                                MainDock.ProjectFiles.ProjectEntries[MainDock.ProjectFiles.ProjectEntries.Count - 1]
                                    as ProjectFolder);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(ex.Message, ex);
        }
    }*/
}