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
using Prism.Ioc;

namespace OneWare.SourceControl.Models;

public class GitRepositoryModel : ObservableObject
{
    private Branch? _headBranch;

    private int _pullCommits;

    private int _pushCommits;

    private string? _workingPath;

    public GitRepositoryModel(IProjectRoot project, Repository repository)
    {
        Project = project;
        Repository = repository;
    }

    public IProjectRoot Project { get; private set; }
    public Repository Repository { get; private set; }

    public ObservableCollection<SourceControlFileModel> Changes { get; set; } = [];

    public ObservableCollection<SourceControlFileModel> StagedChanges { get; set; } = [];

    public ObservableCollection<SourceControlFileModel> MergeChanges { get; set; } = [];

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

    public ObservableCollection<MenuItemViewModel> AvailableBranchesMenu { get; } = new();

    public void Refresh(SourceControlViewModel sourceControlViewModel)
    {
        var changes = new List<SourceControlFileModel>();
        var stagedChanges = new List<SourceControlFileModel>();
        var mergeChanges = new List<SourceControlFileModel>();

        try
        {
            HeadBranch = Repository.Head;

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
            
                AvailableBranchesMenu.Add(menuItem);
            }
            
            AvailableBranchesMenu.Add(new MenuItemViewModel("NewBranch")
            {
                Header = "New Branch...",
                IconObservable = Application.Current!.GetResourceObservable("BoxIcons.RegularGitBranch"),
                //Command = new AsyncRelayCommand(CreateBranchDialogAsync)
            });

            foreach (var item in Repository.RetrieveStatus(new StatusOptions()))
            {
                var fullPath = Path.Combine(Repository.Info.WorkingDirectory, item.FilePath);
                var sModel = new SourceControlFileModel(fullPath, item);

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
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
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