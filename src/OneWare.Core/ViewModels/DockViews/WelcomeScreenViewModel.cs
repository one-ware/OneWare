using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.FolderProjectSystem;
using Prism.Ioc;

namespace OneWare.Core.ViewModels.DockViews;

public class WelcomeScreenViewModel : Document, IWelcomeScreenReceiver
{
    private readonly IPaths _paths;
    private readonly IProjectManagerService _projectManager;

    private bool _recentProjectsAreEmpty = true;

    public WelcomeScreenViewModel(IPaths paths, IProjectManagerService projectManager)
    {
        _paths = paths;
        _projectManager = projectManager;
        Id = "WelcomeScreen";
        Title = "Welcome";
    }
    
    public ObservableCollection<IWelcomeScreenStartItem> NewItems { get; set; } = [];
    public ObservableCollection<IWelcomeScreenStartItem> OpenItems { get; set; } = [];
    public ObservableCollection<IWelcomeScreenWalkthroughItem> WalkthroughItems { get; set; } = [];
    public ObservableCollection<RecentFileViewModel> RecentProjects { get; set; } = [];

    public string Icon => _paths.AppIconPath;
    public string AppName => _paths.AppName;
    public string VersionInfo => $"Version {Global.VersionCode} {RuntimeInformation.ProcessArchitecture}";

    public bool RecentProjectsAreEmpty
    {
        get => _recentProjectsAreEmpty;
        set => SetProperty(ref _recentProjectsAreEmpty, value);
    }
    
    public void LoadRecentProjects()
    {
        var explorerService = ContainerLocator.Container.Resolve<IProjectExplorerService>();
        foreach (var item in explorerService.LoadRecentProjects())
        {
            RecentProjects.Add(new RecentFileViewModel(item));
        }
        RecentProjectsAreEmpty = RecentProjects.Count == 0;
    }
    public async Task OpenRecentFileAsync(RecentFileViewModel item)
    {
        string managerId;
        if (File.Exists(item.Path))
            managerId = "UniversalFPGAProject";
        else if (Directory.Exists(item.Path))
            managerId = "Folder";
        else
            return;
        
        var manager = _projectManager.GetManager(managerId);
        await ContainerLocator.Container.Resolve<IProjectExplorerService>().LoadProjectAsync(item.Path, manager!);
    }
    public void HandleRegisterItemToNew(IWelcomeScreenStartItem item)
    {
        NewItems.Add(item);
    }
    public void HandleRegisterItemToOpen(IWelcomeScreenStartItem item)
    {
        OpenItems.Add(item);
    }
    public void HandleRegisterItemToWalkthrough(IWelcomeScreenWalkthroughItem item)
    {
        WalkthroughItems.Add(item);
    }
}

public class RecentFileViewModel : ObservableObject
{
    private const int MaxCharLength = 40;

    public RecentFileViewModel(string path)
    {
        Name = System.IO.Path.GetFileName(path);
        Path = path;
        ShortenPath = Path.Length > MaxCharLength - Name.Length 
            ? string.Concat(Path[..(MaxCharLength - Name.Length)], "...") 
            : Path;
    }
    
    public string Name { get; } 
    public string Path { get; }
    public string ShortenPath { get; }
}