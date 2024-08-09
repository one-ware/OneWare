using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using LibGit2Sharp;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.Models;
using OneWare.SourceControl.Services;
using Prism.Ioc;

namespace OneWare.SourceControl.ViewModels;

public class SourceControlViewModel : ExtendedTool
{
    public const string IconKey = "BoxIcons.RegularGitBranch";
    private readonly IApplicationStateService _applicationStateService;
    private readonly IDockService _dockService;
    private readonly object _lock = new();

    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IPaths _paths;
    
    private string _commitMessage = "";

    private bool _dllNotFound;

    private Branch? _headBranch;

    private bool _isLoading;

    private int _pullCommits;

    private int _pushCommits;

    private DispatcherTimer? _timer;

    private string _workingPath = "";

    public SourceControlViewModel(ILogger logger, GitService gitService, ISettingsService settingsService,
        IApplicationStateService applicationStateService,
        IDockService dockService, IWindowService windowService,
        IPaths paths,
        IProjectExplorerService projectExplorerService) : base(IconKey)
    {
        _logger = logger;
        GitService = gitService;
        _settingsService = settingsService;
        _applicationStateService = applicationStateService;
        _dockService = dockService;
        _windowService = windowService;
        _paths = paths;
        ProjectExplorerService = projectExplorerService;

        Id = "SourceControl";
        Title = "Source Control";

        RefreshAsyncCommand = new AsyncRelayCommand(RefreshAsync);
        CloneDialogAsyncCommand = new AsyncRelayCommand(CloneDialogAsync);
        SyncAsyncCommand = new AsyncRelayCommand(SyncAsync);
        PullAsyncCommand = new AsyncRelayCommand(PullAsync);
        PushAsyncCommand = new AsyncRelayCommand(PushAsync); // new AsyncRelayCommand(PushAsync);
        FetchAsyncCommand = new AsyncRelayCommand(FetchAsync);
        CommitAsyncCommand = new AsyncRelayCommand<bool>(CommitAsync);
        DiscardAllAsyncCommand = new AsyncRelayCommand<ResetMode>(DiscardAllAsync);
        StageAllCommand = new RelayCommand(StageAll);
        UnStageAllCommand = new RelayCommand(UnStageAll);
        StageCommand = new RelayCommand<string>(Stage);
        UnStageCommand = new RelayCommand<string>(UnStage);
        CreateBranchDialogAsyncCommand = new AsyncRelayCommand(CreateBranchDialogAsync);
        MergeBranchDialogAsyncCommand = new AsyncRelayCommand(MergeBranchDialogAsync);
        DeleteBranchDialogAsyncCommand = new AsyncRelayCommand(DeleteBranchDialogAsync);
        AddRemoteDialogAsyncCommand = new AsyncRelayCommand(AddRemoteDialogAsync);
        DeleteRemoteDialogAsyncCommand = new AsyncRelayCommand(DeleteRemoteDialogAsync);
        SetUserIdentityAsyncCommand = new AsyncRelayCommand<bool>(SetUserIdentityAsync);

        settingsService.GetSettingObservable<int>("SourceControl_AutoFetchDelay")
            .Subscribe(SetupFetchTimer);

        settingsService.GetSettingObservable<int>("SourceControl_PollChangesDelay")
            .Subscribe(SetupPollTimer);

        projectExplorerService
            .WhenValueChanged(x => x.ActiveProject)
            .Subscribe(RefreshAsyncCommand.Execute);
    }
    
    public GitService GitService { get; }

    public IProjectExplorerService ProjectExplorerService { get; }

    public ObservableCollection<SourceControlModel> Changes { get; set; } = new();

    public ObservableCollection<SourceControlModel> StagedChanges { get; set; } = new();

    public ObservableCollection<SourceControlModel> MergeChanges { get; set; } = new();

    public Repository? CurrentRepo { get; private set; }

    public string CommitMessage
    {
        get => _commitMessage;
        set => SetProperty(ref _commitMessage, value);
    }

    public string WorkingPath
    {
        get => _workingPath;
        set => SetProperty(ref _workingPath, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
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

    public AsyncRelayCommand RefreshAsyncCommand { get; }
    public AsyncRelayCommand CloneDialogAsyncCommand { get; }
    public AsyncRelayCommand SyncAsyncCommand { get; }
    public AsyncRelayCommand PullAsyncCommand { get; }
    public AsyncRelayCommand PushAsyncCommand { get; }
    public AsyncRelayCommand FetchAsyncCommand { get; }
    public AsyncRelayCommand<bool> CommitAsyncCommand { get; }
    public AsyncRelayCommand<ResetMode> DiscardAllAsyncCommand { get; }
    public RelayCommand StageAllCommand { get; }
    public RelayCommand UnStageAllCommand { get; }
    public RelayCommand<string> StageCommand { get; }
    public RelayCommand<string> UnStageCommand { get; }
    public AsyncRelayCommand CreateBranchDialogAsyncCommand { get; }
    public AsyncRelayCommand MergeBranchDialogAsyncCommand { get; }
    public AsyncRelayCommand DeleteBranchDialogAsyncCommand { get; }
    public AsyncRelayCommand AddRemoteDialogAsyncCommand { get; }
    public AsyncRelayCommand DeleteRemoteDialogAsyncCommand { get; }
    public AsyncRelayCommand<bool> SetUserIdentityAsyncCommand { get; }

    public ObservableCollection<MenuItemViewModel> AvailableBranchesMenu { get; } = new();

    public void InitializeRepository()
    {
        if (ProjectExplorerService.ActiveProject == null) return;

        lock (_lock)
        {
            try
            {
                Repository.Init(ProjectExplorerService.ActiveProject.RootFolderPath);
                _ = RefreshAsync();
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }
    }

    public async Task CloneDialogAsync()
    {
        var url = await _windowService.ShowInputAsync("Clone",
            "Enter the remote URL for the repository you want to clone", MessageBoxIcon.Info,
            null, _dockService.GetWindowOwner(this));

        if (url == null) return;

        var folder = await _windowService.ShowFolderSelectAsync("Clone",
            "Select the location for the new repository", MessageBoxIcon.Info, _paths.ProjectsDirectory, _dockService.GetWindowOwner(this));

        if (folder == null) return;

        folder = Path.Combine(folder, Path.GetFileNameWithoutExtension(url) ?? "");
        Directory.CreateDirectory(folder);

        var result = await GitService.CloneRepositoryAsync(url, folder);
        
        if (!result) return;

        await Task.Delay(200);

        var startFilePath = await StorageProviderHelper.SelectFilesAsync(_dockService.GetWindowOwner(this)!,
            "Open Project from cloned repository",
            folder);

        foreach (var file in startFilePath)
        {
            //var proj = await MainDock.ProjectFiles.LoadProjectAsync(file);
        }
    }

    public async Task RefreshAsync()
    {
        if (_dllNotFound) return;

        if (ProjectExplorerService.ActiveProject == null) return;

        await WaitUntilFreeAsync();

        lock (_lock)
        {
            IsLoading = true;

            try
            {
                WorkingPath = Repository.Discover(ProjectExplorerService.ActiveProject.RootFolderPath) ??
                              ProjectExplorerService.ActiveProject.RootFolderPath;

                // //Reset
                // foreach (var change in Changes)
                //     if (change.ProjectFile != null)
                //         change.ProjectFile.GitChangeStatus = FileStatus.Unaltered;

                Changes.Clear();
                StagedChanges.Clear();
                MergeChanges.Clear();

                //ContainerLocator.Container.Resolve<ILogger>()?.Log("Refresh GIT " + WorkingPath + " " + Repository.IsValid(WorkingPath), ConsoleColor.Red);          

                HeadBranch = null;
                AvailableBranchesMenu.Clear();

                if (CurrentRepo == null || !CurrentRepo.Info.WorkingDirectory.EqualPaths(WorkingPath))
                {
                    if (Repository.IsValid(WorkingPath))
                        CurrentRepo = new Repository(WorkingPath);
                    else
                        CurrentRepo = null;
                }

                if (CurrentRepo != null && CurrentRepo.Info.Path.EqualPaths(WorkingPath))
                {
                    HeadBranch = CurrentRepo.Head;

                    foreach (var branch in CurrentRepo.Branches)
                    {
                        //if (branch.IsRemote) continue;

                        var menuItem = new MenuItemViewModel("BranchName")
                        {
                            Header = branch.FriendlyName,
                            Command = new RelayCommand<Branch>(ChangeBranch),
                            CommandParameter = branch
                        };
                        if (branch.IsCurrentRepositoryHead)
                        {
                            menuItem.IconObservable = Application.Current!.GetResourceObservable("PicolIcons.Accept");
                            menuItem.IsEnabled = false;
                        }

                        AvailableBranchesMenu.Add(menuItem);
                    }

                    //if (AvailableBranchesMenu.Count > 0)
                    //        AvailableBranchesMenu.Add(new Separator());

                    AvailableBranchesMenu.Add(new MenuItemViewModel("NewBranch")
                    {
                        Header = "New Branch...",
                        IconObservable = Application.Current!.GetResourceObservable("BoxIcons.RegularGitBranch"),
                        Command = new AsyncRelayCommand(CreateBranchDialogAsync)
                    });

                    foreach (var item in CurrentRepo.RetrieveStatus(new StatusOptions()))
                    {
                        var smodel = new SourceControlModel(this, item,
                            ProjectExplorerService.SearchFullPath(Path.Combine(CurrentRepo.Info.WorkingDirectory,
                                    item.FilePath))
                                as IProjectFile);


                        if (item.State.HasFlag(FileStatus.TypeChangeInIndex) ||
                            item.State.HasFlag(FileStatus.RenamedInIndex) ||
                            item.State.HasFlag(FileStatus.DeletedFromIndex) ||
                            item.State.HasFlag(FileStatus.NewInIndex) ||
                            item.State.HasFlag(FileStatus.ModifiedInIndex))
                            StagedChanges.Add(smodel);
                        if (item.State.HasFlag(FileStatus.TypeChangeInWorkdir) ||
                            item.State.HasFlag(FileStatus.RenamedInWorkdir) ||
                            item.State.HasFlag(FileStatus.DeletedFromWorkdir) ||
                            item.State.HasFlag(FileStatus.NewInWorkdir) ||
                            item.State.HasFlag(FileStatus.ModifiedInWorkdir))
                            Changes.Add(smodel);
                        if (item.State.HasFlag(FileStatus.Conflicted)) MergeChanges.Add(smodel);

                        //if (smodel.File is ProjectEntry entry) entry.GitChangeStatus = item.State;
                    }

                    PullCommits = CurrentRepo?.Head.TrackingDetails.BehindBy ?? 0;
                    PushCommits = CurrentRepo?.Head.TrackingDetails.AheadBy ?? 0;
                }
            }
            catch (Exception e)
            {
                if (e is TypeInitializationException) _dllNotFound = true;
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }


            IsLoading = false;
        }
    }

    #region General

    private void SetupFetchTimer(int seconds)
    {
        if (_timer != null && Math.Abs(_timer.Interval.TotalSeconds - seconds) < 1) return;
        _timer?.Stop();
        _timer = new DispatcherTimer(new TimeSpan(0, 0, seconds), DispatcherPriority.Normal, FetchTimerCallback);
        _timer.Start();
    }

    private void FetchTimerCallback(object? sender, EventArgs args)
    {
        if (_settingsService.GetSettingValue<bool>("SourceControl_AutoFetchEnable")) _ = FetchAsync();
    }

    private void SetupPollTimer(int seconds)
    {
        if (_timer != null && Math.Abs(_timer.Interval.TotalSeconds - seconds) < 1) return;
        _timer?.Stop();
        _timer = new DispatcherTimer(new TimeSpan(0, 0, seconds), DispatcherPriority.Normal, PollTimerCallback);
        _timer.Start();
    }

    private void PollTimerCallback(object? sender, EventArgs args)
    {
        if (_settingsService.GetSettingValue<bool>("SourceControl_PollChangesEnable")) _ = RefreshAsync();
    }

    public void ViewInProjectExplorer(IProjectEntry entry)
    {
        _dockService.Show(ProjectExplorerService);
        ProjectExplorerService.ExpandToRoot(entry);
        ProjectExplorerService.SelectedItems.Clear();
        ProjectExplorerService.SelectedItems.Add(entry);
    }

    #endregion

    #region Branches & Remotes

    public void ChangeBranch(Branch? branch)
    {
        if (CurrentRepo == null || branch == null) return;

        try
        {
            if (branch.IsRemote)
            {
                var remoteBranch = branch;
                var branchName = branch.FriendlyName.Split("/");

                if (CurrentRepo.Branches[branchName[1]] is Branch localB)
                {
                    branch = localB;
                }
                else
                {
                    branch = CurrentRepo.CreateBranch(branchName[1], branch.Tip);
                    branch = CurrentRepo.Branches.Update(branch,
                        b => b.TrackedBranch = remoteBranch.CanonicalName);
                }
            }

            Commands.Checkout(CurrentRepo, branch);

            _ = RefreshAsync();

            _logger.Log("Switched to branch '" + branch.FriendlyName + "'", ConsoleColor.Green, true, Brushes.Green);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    public async Task CreateBranchDialogAsync()
    {
        var newBranchName = await _windowService.ShowInputAsync("Create Branch",
            "Please enter a name for the new branch", MessageBoxIcon.Info, null, _dockService.GetWindowOwner(this));
        if (newBranchName != null) CreateBranch(newBranchName);
    }

    public Branch? CreateBranch(string name, bool checkout = true)
    {
        if (CurrentRepo == null || string.IsNullOrWhiteSpace(name)) return null;

        try
        {
            var newBranch = CurrentRepo.CreateBranch(name);

            if (checkout) Commands.Checkout(CurrentRepo, newBranch);

            _ = RefreshAsync();

            return newBranch;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return null;
        }
    }

    public async Task DeleteBranchDialogAsync()
    {
        if (CurrentRepo == null) return;

        var selectedBranchName = await _windowService.ShowInputSelectAsync("Delete Branch",
            "Select the branch you want to delete", MessageBoxIcon.Info,
            CurrentRepo.Branches.Select(x => x.FriendlyName), CurrentRepo.Branches.LastOrDefault()?.FriendlyName,
            _dockService.GetWindowOwner(this)) as string;

        if (selectedBranchName == null) return;

        var deleteBranch = CurrentRepo.Branches
            .FirstOrDefault(x => x.FriendlyName == selectedBranchName);

        if (deleteBranch != null)
        {
            await DeleteBranchAsync(deleteBranch);
            _ = RefreshAsync();
        }
    }

    public async Task<bool> DeleteBranchAsync(Branch branch)
    {
        if (CurrentRepo == null) return false;
        try
        {
            CurrentRepo.Branches.Remove(branch);
            if (branch.IsRemote)
            {
                await WaitUntilFreeAsync();
                
                var cancellationTokenSource = new CancellationTokenSource();
                
                _applicationStateService.AddState("Deleting remote branch", AppState.Loading, () => cancellationTokenSource.Cancel());            
                
                IsLoading = true;

                await Task.Run(() =>
                {
                    var remote = CurrentRepo.Network.Remotes[branch.RemoteName];
                    var pushRefSpec = $"+:refs/heads/{branch.FriendlyName.Split('/')[1]}";
                    var options = new PushOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                            GitService.GetCredentialsAsync(url, usernameFromUrl, types, cancellationTokenSource.Token).Result
                    };
                    CurrentRepo.Network.Push(remote, pushRefSpec, options);
                });
                //Active.SetStatus("Done", Active.AppState.Idle);
                IsLoading = false;
            }

            return true;
        }
        catch (Exception e)
        {
            IsLoading = false;
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return false;
        }
    }

    public async Task MergeBranchDialogAsync()
    {
        if (CurrentRepo == null) return;

        var selectedBranchName = await _windowService.ShowInputSelectAsync("Merge Branch",
            "Select the branch to merge from", MessageBoxIcon.Info,
            CurrentRepo.Branches.Select(x => x.FriendlyName), CurrentRepo.Branches.LastOrDefault()?.FriendlyName,
            _dockService.GetWindowOwner(this)) as string;

        if (selectedBranchName == null) return;

        var mergeBranch = CurrentRepo.Branches
            .FirstOrDefault(x => x.FriendlyName == selectedBranchName);

        if (mergeBranch != null)
            await MergeBranchAsync(mergeBranch);
    }

    public async Task MergeBranchAsync(Branch source)
    {
        if (CurrentRepo == null) return;

        try
        {
            var options = new MergeOptions();
            var result = CurrentRepo.Merge(source.Tip, await GetSignatureAsync(), options);
            if (result != null) PublishMergeResult(result);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    public async Task<bool> PublishBranchDialogAsync()
    {
        if (CurrentRepo == null) return false;

        try
        {
            if (!CurrentRepo.Head.IsTracking)
            {
                var result = await _windowService.ShowYesNoAsync("Info",
                    $"The branch {CurrentRepo.Head.FriendlyName} has no upstream branch. Would you like to publish this branch?",
                    MessageBoxIcon.Info, _dockService.GetWindowOwner(this));
                if (result is MessageBoxStatus.Yes)
                {
                    CurrentRepo.Branches.Update(CurrentRepo.Head,
                        b => b.Remote = CurrentRepo.Network.Remotes.First().Name,
                        b => b.UpstreamBranch = CurrentRepo.Head.CanonicalName);
                    return true;
                }

                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return false;
        }
    }

    public async Task<bool> AddRemoteDialogAsync()
    {
        if (CurrentRepo == null) return false;

        if (!CurrentRepo.Head.IsTracking)
        {
            var url = await _windowService.ShowInputAsync("Add Remote", "Please enter the repository URL",
                MessageBoxIcon.Info,
                null, _dockService.GetWindowOwner(this));
            if (url == null) return false;

            var remoteName = await _windowService.ShowInputAsync("Add Remote",
                "Please enter a name for the remote. If this is the first remote you can leave the name as origin.",
                MessageBoxIcon.Info,
                "origin", _dockService.GetWindowOwner(this));
            if (remoteName == null) return false;

            return AddRemote(url, remoteName);
        }

        return false;
    }

    public bool AddRemote(string url, string name)
    {
        if (CurrentRepo == null || string.IsNullOrEmpty(url) || string.IsNullOrEmpty(name)) return false;
        try
        {
            var remote = CurrentRepo.Network.Remotes.Add(name, url);
            if (remote != null) return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }

        return false;
    }

    public async Task DeleteRemoteDialogAsync()
    {
        if (CurrentRepo == null) return;

        var selectedRemoteName = await _windowService.ShowInputSelectAsync("Delete Remote",
            "Select the remote you want to delete", MessageBoxIcon.Info,
            CurrentRepo.Network.Remotes.Select(x => x.Name), CurrentRepo.Network.Remotes.LastOrDefault()?.Name,
            _dockService.GetWindowOwner(this)) as string;

        if (selectedRemoteName != null)
            DeleteRemote(selectedRemoteName);
    }

    public bool DeleteRemote(string name)
    {
        if (CurrentRepo == null || string.IsNullOrEmpty(name)) return false;
        try
        {
            CurrentRepo.Network.Remotes.Remove(name);
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return false;
        }
    }

    #endregion

    #region Commit & Sync

    public async Task CommitAsync(bool staged)
    {
        if (CurrentRepo == null) return;

        try
        {
            if (!staged) Commands.Stage(CurrentRepo, "*");

            var author = await GetSignatureAsync();
            var committer = author;
            var commit = CurrentRepo.Commit(CommitMessage, author, committer);

            _logger.Log($"Commit {commit.Message}", ConsoleColor.Green, true, Brushes.Green);
            CommitMessage = "";
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }

        _ = RefreshAsync();
        _ = FetchAsync();
    }

    public async Task SyncAsync()
    {
        if (CurrentRepo == null) return;

        var push = true;
        if (!CurrentRepo.Network.Remotes.Any())
        {
            var result = await _windowService.ShowYesNoAsync("Warning",
                "This repository does not have a remote. Do you want to add one?", MessageBoxIcon.Warning,
                _dockService.GetWindowOwner(this));
            if (result is MessageBoxStatus.Yes)
            {
                if (!await AddRemoteDialogAsync()) return;
            }
            else
            {
                return;
            }
        }

        if (!CurrentRepo.Head.IsTracking)
        {
            if (!await PublishBranchDialogAsync()) return;
        }
        else
        {
            var mergeResult = await PullAsync();
            if (mergeResult == null || mergeResult.Status == MergeStatus.Conflicts) push = false;
        }

        if (push)
        {
            var pushResult = await PushAsync();
            if (pushResult)
                //Active.SetStatus("Sync finished successfull", Active.AppState.Idle);
                _windowService.ShowNotification("Success", "Sync finished successfully", NotificationType.Success);
            _ = RefreshAsync();
        }
    }

    public async Task WaitUntilFreeAsync()
    {
        while (IsLoading) await Task.Delay(100);
    }

    public async Task<MergeResult?> PullAsync()
    {
        if (CurrentRepo == null) return null;

        var pullState =
            _applicationStateService.AddState("Pulling from " + CurrentRepo.Head.RemoteName, AppState.Loading);
        
        await WaitUntilFreeAsync();
        IsLoading = true;

        //foreach(var terminal in MainDock.Terminals)
        //{
        //    terminal.CloseConnection();
        //}

        var signature = await GetSignatureAsync();

        var result = await Task.Run(() =>
        {
            try
            {
                // Credential information to fetch
                var options = new PullOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                            GitService.GetCredentialsAsync(url, usernameFromUrl, types).Result
                    }
                };

                var mergeResult = Commands.Pull(CurrentRepo, signature, options);

                return mergeResult;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return null;
            }
        });

        IsLoading = false;
        _applicationStateService.RemoveState(pullState);

        if (result != null)
        {
            _logger.Log($"Pull Status: {result.Status}", default, true);
            PublishMergeResult(result);
        }

        return result;
    }

    public void PublishMergeResult(MergeResult result)
    {
        switch (result.Status)
        {
            case MergeStatus.Conflicts:
                _windowService.ShowNotification("Git Warning",
                    "There are merge conflicts. Resolve them before committing.", NotificationType.Warning);
                _dockService.Show(this);
                break;

            case MergeStatus.UpToDate:
                _windowService.ShowNotification("Git info", "Repository up to date", NotificationType.Information);
                break;

            case MergeStatus.FastForward:
                _windowService.ShowNotification("Git Info", "Pulled changes fast forward",
                    NotificationType.Success);
                break;
        }
    }

    public async Task<bool> PushAsync()
    {
        if (CurrentRepo == null) return false;

        if (!CurrentRepo.Head.IsTracking)
        {
            var success = await PublishBranchDialogAsync();
            if (!success) return false;
        }

        var pullState =
            _applicationStateService.AddState("Pushing to " + CurrentRepo.Head.RemoteName, AppState.Loading);
        await WaitUntilFreeAsync();
        IsLoading = true;

        var result = await Task.Run(() =>
        {
            try
            {
                var pushOptions = new PushOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) =>
                        GitService.GetCredentialsAsync(url, usernameFromUrl, types).Result
                };
                //PUSH                 
                CurrentRepo.Network.Push(CurrentRepo.Head, pushOptions);
                return true;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return false;
            }
        });

        _applicationStateService.RemoveState(pullState);
        IsLoading = false;

        if (result)
        {
            //Global.Factory.ShowDockable(MainDock.Output);
            //MainDock.Output.WriteLine("Pushed successfully to " + CurrentRepo.Head.CanonicalName + "!", Brushes.Green);
        }

        return result;
    }

    public async Task FetchAsync()
    {
        if (CurrentRepo == null) return;


        await WaitUntilFreeAsync();
        IsLoading = true;

        await Task.Run(() =>
        {
            try
            {
                var logMessage = "";
                var options = new FetchOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) =>
                        GitService.GetCredentialsAsync(url, usernameFromUrl, types).Result
                };

                foreach (var remote in CurrentRepo.Network.Remotes)
                {
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    Commands.Fetch(CurrentRepo, remote.Name, refSpecs, options, logMessage);
                }

                PullCommits = CurrentRepo?.Head.TrackingDetails.BehindBy ?? 0;
                PushCommits = CurrentRepo?.Head.TrackingDetails.AheadBy ?? 0;
            }
            catch (Exception e)
            {
                if (_settingsService.GetSettingValue<bool>("SourceControl_AutoFetchEnable"))
                {
                    ContainerLocator.Container.Resolve<ILogger>()
                        ?.Error(e.Message + "\nAutomatic fetching disabled!", e);
                    _settingsService.SetSettingValue("SourceControl_AutoFetchEnable", false);
                }
                else
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
            }
        });

        IsLoading = false;
    }

    #endregion

    #region Stage & Discard

    public void StageAll()
    {
        if (CurrentRepo == null) return;
        Commands.Stage(CurrentRepo, "*");
        _ = RefreshAsync();
    }

    public void UnStageAll()
    {
        if (CurrentRepo == null) return;
        Commands.Unstage(CurrentRepo, "*");
        _ = RefreshAsync();
    }

    public void Stage(string? path)
    {
        if (CurrentRepo == null) return;
        Commands.Stage(CurrentRepo, path);

        _ = RefreshAsync();
    }

    public void UnStage(string? path)
    {
        if (CurrentRepo == null) return;
        Commands.Unstage(CurrentRepo, path);

        _ = RefreshAsync();
    }

    public async Task DiscardAsync(string path)
    {
        if (CurrentRepo == null) return;

        var options = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
        CurrentRepo.CheckoutPaths(CurrentRepo.Head.FriendlyName, new[] { path }, options);

        if (!Path.IsPathRooted(path)) path = Path.Combine(CurrentRepo.Info.WorkingDirectory, path);

        if (CurrentRepo == null || CurrentRepo.Info.WorkingDirectory == null) return;
        var entry = Changes
            .FirstOrDefault(x => Path.Combine(CurrentRepo.Info.WorkingDirectory, x.Status.FilePath) == path);
        if (entry != null && entry.Status.State == FileStatus.NewInWorkdir)
        {
            var result = await _windowService.ShowYesNoCancelAsync("Warning",
                $"Are you sure you want to delete {Path.GetFileName(path)}?", MessageBoxIcon.Warning);

            if (result is MessageBoxStatus.Yes)
                try
                {
                    File.Delete(path);
                    if (ProjectExplorerService.ActiveProject?.SearchFullPath(Path.Combine(
                            CurrentRepo.Info.WorkingDirectory,
                            path)) is IProjectFile file)
                        _ = ProjectExplorerService.RemoveAsync(file);
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
        }

        await RefreshAsync();
    }

    public async Task DiscardAllAsync(ResetMode mode)
    {
        if (CurrentRepo == null) return;
        try
        {
            await WaitUntilFreeAsync();
            CurrentRepo.Reset(mode);

            if (mode == ResetMode.Hard)
            {
                var deleteFiles = new List<string>();
                foreach (var item in CurrentRepo.RetrieveStatus(new StatusOptions()))
                    if (item.State == FileStatus.NewInWorkdir)
                    {
                        var path = Path.Combine(CurrentRepo.Info.WorkingDirectory, item.FilePath);
                        deleteFiles.Add(path);
                    }

                if (deleteFiles.Any())
                {
                    var result = await _windowService.ShowYesNoCancelAsync("Warning",
                        $"Do you want to delete {deleteFiles.Count} untracked files forever?", MessageBoxIcon.Warning);

                    if (result is MessageBoxStatus.Yes)
                        foreach (var f in deleteFiles)
                        {
                            var projFile = ProjectExplorerService.SearchFullPath(f);
                            if (projFile != null) await ProjectExplorerService.DeleteAsync(projFile);
                            else File.Delete(f);
                        }
                }
            }

            _ = RefreshAsync();
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    #endregion

    #region Open & Compare

    public async Task<IFile?> OpenFileAsync(string path)
    {
        if (CurrentRepo?.Info.WorkingDirectory == null) return null;

        if (!Path.IsPathRooted(path)) path = Path.Combine(CurrentRepo.Info.WorkingDirectory, path);

        if (!(ProjectExplorerService.ActiveProject?.SearchFullPath(path) is IFile file))
            file = ProjectExplorerService.GetTemporaryFile(path);

        await _dockService.OpenFileAsync(file);
        return file;
    }

    public async Task OpenHeadFileAsync(string path)
    {
        if (CurrentRepo == null) return;

        string commitContent;
        var blob = CurrentRepo.Head.Tip[path].Target as Blob;

        if (blob == null) throw new NullReferenceException(nameof(blob));

        using (var content = new StreamReader(blob.GetContentStream(), Encoding.UTF8))
        {
            commitContent = await content.ReadToEndAsync();
        }

        var file = ProjectExplorerService.GetTemporaryFile(path);

        var evm = await _dockService.OpenFileAsync(file);

        if (evm is IEditor editor)
        {
            editor.Title += " (HEAD)";
            editor.IsReadOnly = true;
            editor.CurrentDocument.Text = commitContent;
        }
    }

    public void CompareAndSwitch(string path)
    {
        Compare(path, true);
    }

    public void Compare(string path, bool switchTab)
    {
        _ = CompareChangesAsync(path, "Diff: ", 10000, switchTab);
    }

    public void ViewChanges(string path)
    {
        _ = CompareChangesAsync(path, "Changes: ");
    }

    public Patch? GetPatch(string path, int contextLines)
    {
        return CurrentRepo?.Diff.Compare<Patch>(new List<string> { path }, false,
            new ExplicitPathsOptions(),
            new CompareOptions { ContextLines = contextLines });
    }

    public async Task CompareChangesAsync(string path, string titlePrefix, int contextLines = 3,
        bool switchTab = true)
    {
        if (CurrentRepo == null) return;
        await WaitUntilFreeAsync();
        try
        {
            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(CurrentRepo.Info.WorkingDirectory, path.Replace('/', Path.DirectorySeparatorChar));

            var openTab = _dockService.SearchView<CompareFileViewModel>().FirstOrDefault(x => x.FullPath == fullPath);
            openTab ??= ContainerLocator.Container.Resolve<CompareFileViewModel>((typeof(string), fullPath));

            openTab.Title = titlePrefix + Path.GetFileName(path);
            openTab.Id = titlePrefix + fullPath;

            _dockService.Show(openTab, DockShowLocation.Document);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    #endregion

    #region Merge

    public async Task MergeAcceptIncomingAsync(string path)
    {
        await MergeAllAsync(path, MergeMode.KeepIncoming);
    }

    public async Task MergeAcceptCurrentAsync(string path)
    {
        await MergeAllAsync(path, MergeMode.KeepCurrent);
    }

    public async Task MergeAllAsync(string path, MergeMode mode)
    {
        var file = await OpenFileAsync(path);
        if (file == null) return;

        var evm = await _dockService.OpenFileAsync(file);
        if (evm is IEditor editor)
        {
            var merges = MergeService.GetMerges(editor.CurrentDocument);
            merges.Reverse(); //Reverse to avoid mistakes with wrong index
            foreach (var merge in merges) MergeService.Merge(editor.CurrentDocument, merge, mode);
        }
    }

    #endregion

    #region Credentials & Identity

    public async Task<Signature?> GetSignatureAsync()
    {
        if (CurrentRepo == null) return null;

        var author = CurrentRepo.Config.BuildSignature(DateTimeOffset.Now);

        if (author == null)
        {
            var identity = await SetUserIdentityAsync(true);

            author = new Signature(identity, DateTime.Now);
        }

        return author;
    }

    public async Task<Identity?> GetIdentityManualAsync()
    {
        if (CurrentRepo == null) return null;

        var author = CurrentRepo.Config.BuildSignature(DateTimeOffset.Now);

        var name = await _windowService.ShowInputAsync("Info", "Please enter a name to sign your changes",
            MessageBoxIcon.Info, author?.Name, _dockService.GetWindowOwner(this));
        if (name == null) return null;

        var email = await _windowService.ShowInputAsync("Info",
            "Please enter a valid email adress to sign your changes", MessageBoxIcon.Info, author?.Email,
            _dockService.GetWindowOwner(this));
        if (email == null) return null;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            _logger.Error("Username and/or email can't be empty", null, false, true);
            return null;
        }

        return new Identity(name, email);
    }

    public async Task<Identity?> SetUserIdentityAsync(bool dialog)
    {
        if (CurrentRepo == null) return null;

        var identity = await GetIdentityManualAsync();

        if (identity == null) return null;

        var result = dialog
            ? await _windowService.ShowYesNoAsync("Info",
                "Do you want to save this information in your global git configuration so that you do not have to enter them again next time?",
                MessageBoxIcon.Info, _dockService.GetWindowOwner(this))
            : MessageBoxStatus.Yes;

        if (result is MessageBoxStatus.Yes)
        {
            if (!CurrentRepo.Config.HasConfig(ConfigurationLevel.Global))
            {
                try
                {
                    var globalConfig =
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".gitconfig");
                    File.WriteAllText(globalConfig,
                        $"[user]\n\tname = {identity.Name}\n\temail = {identity.Email}\n", Encoding.UTF8);
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
            }
            else
            {
                CurrentRepo.Config.Set("user.name", identity.Name, ConfigurationLevel.Global);
                CurrentRepo.Config.Set("user.email", identity.Email, ConfigurationLevel.Global);
            }
        }

        return identity;
    }

    #endregion
}