using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;
using Prism.Ioc;
using IDockService = OneWare.Essentials.Services.IDockService;

namespace OneWare.LibraryExplorer.ViewModels;

public class LibraryExplorerViewModel : ProjectViewModelBase
{
    public const string IconKey = "BoxIcons.RegularLibrary";

    private string _libraryFolderPath;
    
    private readonly IFileWatchService _fileWatchService;
    private readonly IDockService _dockService;
    private readonly IProjectExplorerService _projectExplorerService;

    public LibraryExplorerViewModel(IPaths paths, IFileWatchService fileWatchService, IDockService dockService, IProjectExplorerService projectExplorerService) : base(IconKey)
    {
        Id = "LibraryExplorer";
        Title = "Library Explorer";
        
        _fileWatchService = fileWatchService;
        _dockService = dockService;
        _projectExplorerService = projectExplorerService;

        _libraryFolderPath = Path.Combine(paths.PackagesDirectory, "Libraries");
        
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

        Directory.CreateDirectory(_libraryFolderPath);
        var directories = Directory.EnumerateDirectories(_libraryFolderPath);

        Projects.Clear();

        foreach (var dir in directories)
        {
            var root = await manager.LoadProjectAsync(dir);
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
            }
        }
        if (SelectedItems.Count > 0)
        {
            menuItems.Add(new MenuItemViewModel("Copy to Project")
            {
                Header = "Copy to Active Project",
                Command = new AsyncRelayCommand(() => CopyLibraryAsync(SelectedItems.Cast<IProjectEntry>()
                    .ToArray()), () => _projectExplorerService.ActiveProject != null)
            });
        }
        else
        {
            menuItems.Add(new MenuItemViewModel("Refresh")
            {
                Header = "Refresh",
                Command = new RelayCommand(() => PlatformHelper.OpenExplorerPath(_libraryFolderPath))
            });
            menuItems.Add(new MenuItemViewModel("Open Library Folder")
            {
                Header = "Open Library Folder",
                Command = new RelayCommand(() => PlatformHelper.OpenExplorerPath(_libraryFolderPath))
            });
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

    private async Task CopyLibraryAsync(params IProjectEntry[] entries)
    {
        var proj = _projectExplorerService.ActiveProject;
        
        if(proj == null) return;

        var libFolder = proj.AddFolder("lib");

        await _projectExplorerService.ImportAsync(libFolder, true, true, entries.Select(x => x.FullPath).ToArray());
    }
}