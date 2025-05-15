using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibGit2Sharp;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.ViewModels;
using Autofac;

namespace OneWare.SourceControl.Models;

public class GitRepositoryModel : ObservableObject
{
    private Branch? _headBranch;
    private int _pullCommits;
    private int _pushCommits;
    private string? _workingPath;

    // Constructor with dependency injection via Autofac
    public GitRepositoryModel(IProjectRoot project, Repository repository, IProjectExplorerService projectExplorerService, ILogger logger)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _projectExplorerService = projectExplorerService ?? throw new ArgumentNullException(nameof(projectExplorerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IProjectRoot Project { get; private set; }
    public Repository Repository { get; private set; }

    public ObservableCollection<SourceControlFileModel> Changes { get; set; } = new ObservableCollection<SourceControlFileModel>();
    public ObservableCollection<SourceControlFileModel> StagedChanges { get; set; } = new ObservableCollection<SourceControlFileModel>();
    public ObservableCollection<SourceControlFileModel> MergeChanges { get; set; } = new ObservableCollection<SourceControlFileModel>();

    public string? WorkingPath
    {
        get => _workingPath;
        set => SetProperty(ref _workingPath, value);
    }

    public Branch? HeadBranch
    {
        get => _headBranch;
        set => SetProperty(ref _headBranch, value);
    }

    public int PullCommits
    {
        get => _pullCommits;
        set => SetProperty(ref _pullCommits, value);
    }

    public int PushCommits
    {
        get => _pushCommits;
        set => SetProperty(ref _pushCommits, value);
    }

    public ObservableCollection<MenuItemViewModel> AvailableBranchesMenu { get; } = new ObservableCollection<MenuItemViewModel>();

    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ILogger _logger;

    public void Refresh(SourceControlViewModel sourceControlViewModel)
    {
        var changes = new List<SourceControlFileModel>();
        var stagedChanges = new List<SourceControlFileModel>();
        var mergeChanges = new List<SourceControlFileModel>();

        try
        {
            HeadBranch = Repository.Head;

            var branchesMenu = new List<MenuItemViewModel>();

            foreach (var branch in Repository.Branches)
            {
                var menuItem = new MenuItemViewModel("BranchName")
                {
                    Header = branch.FriendlyName,
                    Command = new RelayCommand(() => sourceControlViewModel.ChangeBranch(branch)),
                };
                if (branch.IsCurrentRepositoryHead)
                {
                    menuItem.IconObservable = Application.Current!.GetResourceObservable("PicolIcons.Accept");
                    menuItem.IsEnabled = false;
                }

                branchesMenu.Add(menuItem);
            }

            branchesMenu.Add(new MenuItemViewModel("NewBranch")
            {
                Header = "New Branch...",
                IconObservable = Application.Current!.GetResourceObservable("BoxIcons.RegularGitBranch"),
                Command = sourceControlViewModel.CreateBranchDialogAsyncCommand
            });

            AvailableBranchesMenu.Merge(branchesMenu, (a, b) =>
            {
                var equal = a.Name == b.Name;

                if (equal)
                {
                    a.IconObservable = b.IconObservable;
                    a.IsEnabled = b.IsEnabled;
                    a.Command = b.Command;
                    a.CommandParameter = b.CommandParameter;
                }
                return equal;
            },
                (a, b) =>
                {
                    if (a.Name == "New Branch...") return -1;
                    if (b.Name == "New Branch...") return 1;

                    var aTracking = a.Name.StartsWith("origin/");
                    var bTracking = b.Name.StartsWith("origin/");

                    return aTracking switch
                    {
                        true when !bTracking => -1,
                        false when bTracking => 1,
                        _ => string.Compare(a.Name, b.Name, StringComparison.Ordinal)
                    };
                });

            foreach (var item in Repository.RetrieveStatus(new StatusOptions()))
            {
                var fullPath = Path.Combine(Repository.Info.WorkingDirectory, item.FilePath);

                var sModel = new SourceControlFileModel(fullPath, item)
                {
                    ProjectFile = _projectExplorerService.SearchFullPath(fullPath) as IProjectFile
                };

                if (item.State.HasFlag(FileStatus.TypeChangeInIndex) ||
                    item.State.HasFlag(FileStatus.RenamedInIndex) ||
                    item.State.HasFlag(FileStatus.DeletedFromIndex) ||
                    item.State.HasFlag(FileStatus.NewInIndex) ||
                    item.State.HasFlag(FileStatus.ModifiedInIndex))
                {
                    stagedChanges.Add(sModel);
                }

                if (item.State.HasFlag(FileStatus.TypeChangeInWorkdir) ||
                    item.State.HasFlag(FileStatus.RenamedInWorkdir) ||
                    item.State.HasFlag(FileStatus.DeletedFromWorkdir) ||
                    item.State.HasFlag(FileStatus.NewInWorkdir) ||
                    item.State.HasFlag(FileStatus.ModifiedInWorkdir))
                {
                    changes.Add(sModel);
                }

                if (item.State.HasFlag(FileStatus.Conflicted))
                {
                    mergeChanges.Add(sModel);
                }
            }

            PullCommits = Repository?.Head.TrackingDetails.BehindBy ?? 0;
            PushCommits = Repository?.Head.TrackingDetails.AheadBy ?? 0;
        }
        catch (Exception e)
        {
            _logger?.Error(e.Message, e);
        }

        StagedChanges.Merge(stagedChanges, (a, b) =>
        {
            var equal = a.Status.FilePath == b.Status.FilePath;
            if (equal)
            {
                a.Status = b.Status;
                return true;
            }

            return false;
        }, (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        MergeChanges.Merge(mergeChanges, (a, b) =>
        {
            var equal = a.Status.FilePath == b.Status.FilePath;
            if (equal)
            {
                a.Status = b.Status;
                return true;
            }

            return false;
        }, (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        Changes.Merge(changes, (a, b) =>
        {
            var equal = a.Status.FilePath == b.Status.FilePath;
            if (equal)
            {
                a.Status = b.Status;
                return true;
            }

            return false;
        }, (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
    }
}
