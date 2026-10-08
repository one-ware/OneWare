using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.ViewModels;

namespace OneWare.SourceControl.Models;

public class GitRepositoryModel : ObservableObject, IDisposable
{
    private Branch? _headBranch;

    private int _pullCommits;

    private int _pushCommits;

    private string? _workingPath;

    public GitRepositoryModel(IProjectRoot project, Repository repository)
    {
        Project = project;
        Repository = repository;
        WorkingPath = repository.Info.WorkingDirectory;
    }

    public IProjectRoot Project { get; private set; }
    public Repository Repository { get; }

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

    public ObservableCollection<MenuItemModel> AvailableBranchesMenu { get; } = new();

    public const int HistoryPageSize = 200;

    private GitGraphBuilder _graphBuilder = new();
    private string? _branchSignature;
    private string? _historySignature;
    private int _historyCount;

    public ObservableCollection<GitGraphRow> History { get; } = [];

    public bool HasMoreHistory
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool ShowAllBranches
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) _historySignature = null;
        }
    }

    public async Task RefreshAsync(SourceControlViewModel sourceControlViewModel)
    {
        var changes = new List<SourceControlFileModel>();
        var stagedChanges = new List<SourceControlFileModel>();
        var mergeChanges = new List<SourceControlFileModel>();

        try
        {
            var knownHistorySignature = _historySignature;
            var historyCount = Math.Max(_historyCount, HistoryPageSize);
            var showAllBranches = ShowAllBranches;
            FileIconService ??= sourceControlViewModel.FileIconService;

            // Scan the worktree off the UI thread; publish observable state only after it completes.
            var snapshot = await Task.Run(() =>
            {
                var head = Repository.Head;
                var branches = Repository.Branches.ToArray();
                var historySignature = GetHistorySignature(head, branches, showAllBranches);
                return (
                    Head: head,
                    Branches: branches,
                    Status: Repository.RetrieveStatus(new StatusOptions()).ToArray(),
                    Behind: head.TrackingDetails.BehindBy ?? 0,
                    Ahead: head.TrackingDetails.AheadBy ?? 0,
                    HistorySignature: historySignature,
                    History: historySignature == knownHistorySignature
                        ? null
                        : LoadHistory(head, branches, showAllBranches, 0, historyCount));
            });

            // Only publish a new branch when it actually moved, the header and status bar bind to it.
            if (HeadBranch == null || HeadBranch.CanonicalName != snapshot.Head.CanonicalName ||
                HeadBranch.Tip?.Sha != snapshot.Head.Tip?.Sha || HeadBranch.IsTracking != snapshot.Head.IsTracking)
                HeadBranch = snapshot.Head;

            UpdateBranchMenu(sourceControlViewModel, snapshot.Branches);

            if (snapshot.History is { } history)
            {
                _historySignature = snapshot.HistorySignature;
                PublishHistory(history, true);
            }

            foreach (var item in snapshot.Status)
            {
                var fullPath = Path.Combine(Repository.Info.WorkingDirectory, item.FilePath);

                var sModel = new SourceControlFileModel(fullPath, item)
                {
                    FileIcon = sourceControlViewModel.FileIconService.GetFileIconModel(Path.GetExtension(fullPath))
                };

                if (item.State.HasFlag(FileStatus.Conflicted))
                {
                    mergeChanges.Add(sModel);
                    continue;
                }

                if (item.State.HasFlag(FileStatus.TypeChangeInIndex) ||
                    item.State.HasFlag(FileStatus.RenamedInIndex) ||
                    item.State.HasFlag(FileStatus.DeletedFromIndex) ||
                    item.State.HasFlag(FileStatus.NewInIndex) ||
                    item.State.HasFlag(FileStatus.ModifiedInIndex))
                    stagedChanges.Add(sModel);

                if (item.State.HasFlag(FileStatus.TypeChangeInWorkdir) ||
                    item.State.HasFlag(FileStatus.RenamedInWorkdir) ||
                    item.State.HasFlag(FileStatus.DeletedFromWorkdir) ||
                    item.State.HasFlag(FileStatus.NewInWorkdir) ||
                    item.State.HasFlag(FileStatus.ModifiedInWorkdir))
                    changes.Add(sModel);

            }

            PullCommits = snapshot.Behind;
            PushCommits = snapshot.Ahead;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return;
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

    /// <summary>
    ///     Loads the next page of history. Must be called while the repository is not used elsewhere.
    /// </summary>
    public async Task LoadMoreHistoryAsync()
    {
        if (!HasMoreHistory) return;
        var skip = _historyCount;
        var showAllBranches = ShowAllBranches;
        try
        {
            var page = await Task.Run(() =>
                LoadHistory(Repository.Head, Repository.Branches.ToArray(), showAllBranches, skip, HistoryPageSize));
            PublishHistory(page, false);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    private void UpdateBranchMenu(SourceControlViewModel sourceControlViewModel, Branch[] branches)
    {
        var signature = string.Join("\n", branches.Select(x => x.CanonicalName + (x.IsCurrentRepositoryHead ? "*" : "")));
        if (signature == _branchSignature) return;
        _branchSignature = signature;

        var branchesMenu = new List<MenuItemModel>();

        foreach (var branch in branches)
        {
            var canonicalName = branch.CanonicalName;
            var menuItem = new MenuItemModel(canonicalName)
            {
                Header = branch.FriendlyName,
                Command = new RelayCommand(() => sourceControlViewModel.ChangeBranch(canonicalName))
            };
            if (branch.IsCurrentRepositoryHead)
            {
                menuItem.Icon = new IconModel("PicolIcons.Accept");
                menuItem.IsEnabled = false;
            }

            branchesMenu.Add(menuItem);
        }

        branchesMenu.Add(new MenuItemModel("NewBranch")
        {
            Header = "New Branch...",
            Icon = new IconModel("BoxIcons.RegularGitBranch"),
            Command = sourceControlViewModel.CreateBranchDialogAsyncCommand
        });

        AvailableBranchesMenu.Merge(branchesMenu, (a, b) =>
            {
                var equal = a.PartId == b.PartId;

                if (equal)
                {
                    if ((a.Icon == null) != (b.Icon == null)) a.Icon = b.Icon;
                    a.IsEnabled = b.IsEnabled;
                }

                return equal;
            },
            (a, b) =>
            {
                if (a.PartId == "NewBranch") return 1;
                if (b.PartId == "NewBranch") return -1;

                var aRemote = a.PartId.StartsWith("refs/remotes/", StringComparison.Ordinal);
                var bRemote = b.PartId.StartsWith("refs/remotes/", StringComparison.Ordinal);

                return aRemote switch
                {
                    true when !bRemote => 1,
                    false when bRemote => -1,
                    _ => string.Compare(a.PartId, b.PartId, StringComparison.Ordinal)
                };
            });
    }

    private sealed record HistoryPage(
        List<GitCommitInfo> Commits,
        Dictionary<string, List<GitRefInfo>> Refs,
        string? HeadSha,
        bool HasMore);

    private string GetHistorySignature(Branch head, Branch[] branches, bool showAllBranches)
    {
        var tags = Repository.Tags.Select(x => x.CanonicalName + ":" + x.Target.Sha);
        return string.Join("\n",
            branches.Select(x => x.CanonicalName + ":" + x.Tip?.Sha).Concat(tags)
                .Append("HEAD:" + head.CanonicalName + ":" + head.Tip?.Sha + ":" + showAllBranches));
    }

    private HistoryPage LoadHistory(Branch head, Branch[] branches, bool showAllBranches, int skip, int count)
    {
        var refs = new Dictionary<string, List<GitRefInfo>>();

        void AddRef(string? sha, GitRefInfo info)
        {
            if (sha == null) return;
            if (!refs.TryGetValue(sha, out var list)) refs[sha] = list = [];
            list.Add(info);
        }

        if (head.Tip == null) return new HistoryPage([], refs, null, false);

        if (Repository.Info.IsHeadDetached) AddRef(head.Tip.Sha, new GitRefInfo("HEAD", GitRefKind.Head, true));
        foreach (var branch in branches)
        {
            if (branch.IsRemote && branch.FriendlyName.EndsWith("/HEAD", StringComparison.Ordinal)) continue;
            AddRef(branch.Tip?.Sha, new GitRefInfo(branch.FriendlyName,
                branch.IsRemote ? GitRefKind.RemoteBranch : GitRefKind.LocalBranch, branch.IsCurrentRepositoryHead));
        }

        foreach (var tag in Repository.Tags)
            AddRef((tag.PeeledTarget as Commit)?.Sha, new GitRefInfo(tag.FriendlyName, GitRefKind.Tag));

        var tips = new List<Commit> { head.Tip };
        if (showAllBranches)
            tips.AddRange(branches.Where(x => x.Tip != null).Select(x => x.Tip));
        else if (head.TrackedBranch?.Tip is { } upstream)
            tips.Add(upstream);

        var filter = new CommitFilter
        {
            IncludeReachableFrom = tips.DistinctBy(x => x.Sha).ToList(),
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time
        };

        var commits = Repository.Commits.QueryBy(filter).Skip(skip).Take(count + 1)
            .Select(x => new GitCommitInfo(x.Sha, x.MessageShort, x.Message, x.Author.Name, x.Author.Email,
                x.Author.When, x.Parents.Select(p => p.Sha).ToArray()))
            .ToList();

        var hasMore = commits.Count > count;
        if (hasMore) commits.RemoveAt(commits.Count - 1);

        foreach (var list in refs.Values)
            list.Sort((a, b) => a.IsCurrent != b.IsCurrent ? (a.IsCurrent ? -1 : 1) : a.Kind.CompareTo(b.Kind));

        return new HistoryPage(commits, refs, head.Tip.Sha, hasMore);
    }

    private void PublishHistory(HistoryPage page, bool reset)
    {
        HashSet<string>? expanded = null;
        if (reset)
        {
            expanded = History.Where(x => x.IsExpanded).Select(x => x.Commit.Sha).ToHashSet();
            _graphBuilder = new GitGraphBuilder();
            _historyCount = 0;
        }

        var rows = _graphBuilder.Append(page.Commits,
            c => page.Refs.TryGetValue(c.Sha, out var list) ? list : [], page.HeadSha);

        _historyCount += page.Commits.Count;
        HasMoreHistory = page.HasMore;

        if (reset)
        {
            History.Clear();
        }

        foreach (var row in rows)
        {
            History.Add(row);
            if (expanded?.Contains(row.Commit.Sha) == true) _ = SetExpandedAsync(row, true);
        }
    }

    public async Task SetExpandedAsync(GitGraphRow row, bool expanded)
    {
        row.IsExpanded = expanded;
        if (!expanded || row.Files.Count > 0 || row.IsLoadingFiles) return;

        row.IsLoadingFiles = true;
        try
        {
            var gitDirectory = Repository.Info.Path;
            var workingDirectory = Repository.Info.WorkingDirectory ?? string.Empty;
            // A separate handle keeps this independent from operations running on the shared repository.
            var changes = await Task.Run(() =>
            {
                using var repository = new Repository(gitDirectory);
                return GitOperations.GetCommitChanges(repository, row.Commit.Sha);
            });

            foreach (var change in changes.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
            {
                var fullPath = Path.Combine(workingDirectory, change.Path.Replace('/', Path.DirectorySeparatorChar));
                row.Files.Add(new GitCommitFileModel(row, change.Path,
                    change.OldPath != change.Path ? change.OldPath : null, change.Status, fullPath)
                {
                    FileIcon = FileIconService?.GetFileIconModel(Path.GetExtension(change.Path))
                });
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
        finally
        {
            row.IsLoadingFiles = false;
        }
    }

    public IFileIconService? FileIconService { get; set; }

    public void Dispose() => Repository.Dispose();
}
