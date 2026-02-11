using System.IO;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem;
using OneWare.ProjectExplorer.ViewModels;

namespace OneWare.LibraryExplorer.ViewModels;

public class LibraryExplorerViewModel : ProjectViewModelBase
{
    public const string IconKey = "BoxIcons.RegularLibrary";

    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;

    private readonly string _libraryFolderPath;

    public LibraryExplorerViewModel(IPaths paths, IMainDockService mainDockService,
        IProjectExplorerService projectExplorerService) : base(IconKey)
    {
        Id = "LibraryExplorer";
        Title = "Libraries";

        _mainDockService = mainDockService;
        _projectExplorerService = projectExplorerService;

        _libraryFolderPath = Path.Combine(paths.PackagesDirectory, "Libraries");

        _ = LoadAsync();
    }

    public override void AddProject(IProjectRoot project)
    {
        base.AddProject(project);
    }

    public void DoubleTab()
    {
        if (SelectedItems is [IProjectFile file])
            _ = PreviewFileAsync(file);
        else if (SelectedItems is [IProjectFolder folder])
            folder.IsExpanded = !folder.IsExpanded;
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
            AddProject(root!);
        }
    }

    public void ConstructContextMenu(TopLevel topLevel)
    {
        var menuItems = new List<MenuItemModel>();

        if (SelectedItems is [{ } item])
            switch (item)
            {
                case IProjectFile file:
                    menuItems.Add(new MenuItemModel("Open")
                    {
                        Header = "Open",
                        Command = new AsyncRelayCommand(() => PreviewFileAsync(file))
                    });
                    break;
            }

        if (SelectedItems.Count > 0)
        {
            menuItems.Add(new MenuItemModel("Copy to Project")
            {
                Header = "Copy to Active Project",
                Command = new AsyncRelayCommand(() => CopyLibraryAsync(SelectedItems.Cast<IProjectEntry>()
                    .ToArray()), () => _projectExplorerService.ActiveProject != null)
            });
        }
        else
        {
            menuItems.Add(new MenuItemModel("Refresh")
            {
                Header = "Refresh",
                Command = new AsyncRelayCommand(async () => await LoadAsync())
            });
            menuItems.Add(new MenuItemModel("Open Library Folder")
            {
                Header = "Open Library Folder",
                Command = new RelayCommand(() => PlatformHelper.OpenExplorerPath(_libraryFolderPath))
            });
        }

        TreeViewContextMenu = menuItems;
    }

    private async Task PreviewFileAsync(IProjectFile file)
    {
        var extendedDocument = await _mainDockService.OpenFileAsync(file.FullPath);
        if (extendedDocument != null)
        {
            extendedDocument.IsReadOnly = true;
            extendedDocument.Title = "PREVIEW: " + Path.GetFileName(extendedDocument.FullPath);
        }
    }

    private async Task CopyLibraryAsync(params IProjectEntry[] entries)
    {
        var proj = _projectExplorerService.ActiveProject;

        if (proj == null) return;

        var libFolder = proj.AddFolder("lib");

        await _projectExplorerService.ImportAsync(libFolder, true, true, entries.Select(x => x.FullPath).ToArray());
    }
}
