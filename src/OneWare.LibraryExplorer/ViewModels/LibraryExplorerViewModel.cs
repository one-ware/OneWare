using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;
using Prism.Ioc;

namespace OneWare.LibraryExplorer.ViewModels;

public class LibraryExplorerViewModel : ProjectViewModelBase
{
    public const string IconKey = "BoxIcons.RegularLibrary";

    private readonly IPaths _paths;
    private readonly IFileWatchService _fileWatchService;
    private readonly IDockService _dockService;

    public LibraryExplorerViewModel(IPaths paths, IFileWatchService fileWatchService, IDockService dockService) : base(IconKey)
    {
        Id = "LibraryExplorer";
        Title = "Library Explorer";

        _paths = paths;
        _fileWatchService = fileWatchService;
        _dockService = dockService;
        
        _ = LoadAsync();
    }
    
    public override void Insert(IProjectRoot project)
    {
        base.Insert(project);
        _fileWatchService.Register(project);
    }
    
    public void DoubleTab(IProjectEntry entry)
    {
        if (entry is IProjectFile file)
            _ = PreviewFileAsync(file);
        else
            entry.IsExpanded = !entry.IsExpanded;
    }

    public async Task LoadAsync()
    {
        var manager = ContainerLocator.Container.Resolve<FolderProjectManager>();
        var libraryPath = Path.Combine(_paths.PackagesDirectory, "Libraries");

        Directory.CreateDirectory(libraryPath);
        var directories = Directory.EnumerateDirectories(libraryPath);

        foreach (var dir in directories)
        {
            var root = await manager.LoadProjectAsync(libraryPath);
            Insert(root!);
        }
    }

    public void ConstructContextMenu(TopLevel topLevel)
    {
        var menuItems = new List<MenuItemViewModel>();

        if (SelectedItems is [{ } item])
        {
            switch (item)
            {
                case IProjectFile file:
                    menuItems.Add(new MenuItemViewModel("Open")
                    {
                        Header = "Open",
                        Command = new AsyncRelayCommand(() => PreviewFileAsync(file))
                    });
                    break;
                case IProjectFolder folder:
                    
                    break;
            }
        }
        
        TreeViewContextMenu = menuItems;
    }

    private async Task PreviewFileAsync(IProjectFile file)
    {
        var extendedDocument = await _dockService.OpenFileAsync(file);
        if (extendedDocument != null)
        {
            extendedDocument.IsReadOnly = true;
            extendedDocument.Title = "PREVIEW: " + extendedDocument.CurrentFile?.Name;
        }
    }
}