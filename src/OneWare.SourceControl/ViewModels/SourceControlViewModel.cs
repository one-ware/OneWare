using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using GitCredentialManager;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.LoginProviders;
using OneWare.SourceControl.Models;
using OneWare.SourceControl.Views;

namespace OneWare.SourceControl.ViewModels;

public class SourceControlViewModel : ExtendedTool
{
    public const string IconKey = "BoxIcons.RegularGitBranch";
    private readonly IApplicationStateService _applicationStateService;

    private readonly ILogger _logger;

    private readonly Dictionary<string, ILoginProvider> _loginProviders = new();
    private readonly IMainDockService _mainDockService;
    private readonly IPaths _paths;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

    private GitRepositoryModel? _activeRepository;

    private string _commitMessage = "";

    private bool _isLoading;

    private DispatcherTimer? _timer;

    public SourceControlViewModel(ILogger logger, ISettingsService settingsService,
        IApplicationStateService applicationStateService,
        IMainDockService mainDockService, IWindowService windowService,
        IPaths paths,
        IProjectExplorerService projectExplorerService,
        IApplicationCommandService applicationCommandService) : base(IconKey)
    {
        _logger = logger;
        _settingsService = settingsService;
        _applicationStateService = applicationStateService;
        _mainDockService = mainDockService;
        _windowService = windowService;
        _projectExplorerService = projectExplorerService;
        _paths = paths;
        _projectExplorerService = projectExplorerService;

        Id = "SourceControl";
        Title = "Source Control";

        InitializeRepositoryCommand =
            new RelayCommand(InitializeRepository, () => _projectExplorerService.ActiveProject != null);
        RefreshAsyncCommand = new AsyncRelayCommand(RefreshAsync);
        CloneDialogAsyncCommand = new AsyncRelayCommand(CloneDialogAsync);
        SyncAsyncCommand = new AsyncRelayCommand(SyncAsync, () => ActiveRepository != null);
        PullAsyncCommand = new AsyncRelayCommand(PullAsync);
        PushAsyncCommand = new AsyncRelayCommand(PushAsync); // new AsyncRelayCommand(PushAsync);
        FetchAsyncCommand = new AsyncRelayCommand(FetchAsync);
        CommitAsyncCommand = new AsyncRelayCommand<bool>(CommitAsync);
        DiscardAllAsyncCommand = new AsyncRelayCommand<ResetMode>(DiscardAllAsync);
        StageAllCommand = new RelayCommand(StageAll);
        UnStageAllCommand = new RelayCommand(UnStageAll);
        StageCommand = new RelayCommand<string>(Stage);
        UnStageCommand = new RelayCommand<string>(UnStage);
        CreateBranchDialogAsyncCommand = new AsyncRelayCommand(CreateBranchDialogAsync, () => ActiveRepository != null);
        MergeBranchDialogAsyncCommand = new AsyncRelayCommand(MergeBranchDialogAsync);
        DeleteBranchDialogAsyncCommand = new AsyncRelayCommand(DeleteBranchDialogAsync);
        AddRemoteDialogAsyncCommand = new AsyncRelayCommand(AddRemoteDialogAsync);
        DeleteRemoteDialogAsyncCommand = new AsyncRelayCommand(DeleteRemoteDialogAsync);
        SetUserIdentityAsyncCommand = new AsyncRelayCommand<bool>(SetUserIdentityAsync);

        settingsService.GetSettingObservable<double>("SourceControl_AutoFetchDelay")
            .Subscribe(SetupFetchTimer);

        settingsService.GetSettingObservable<double>("SourceControl_PollChangesDelay")
            .Subscribe(SetupPollTimer);

        projectExplorerService
            .WhenValueChanged(x => x.ActiveProject)
            .Subscribe(RefreshAsyncCommand.Execute);

        _loginProviders.Add("github.com", ContainerLocator.Container.Resolve<GithubLoginProvider>());

        applicationCommandService.RegisterCommand(new CommandApplicationCommand("GIT Sync", SyncAsyncCommand)
        {
            IconObservable = Application.Current!.GetResourceObservable("VsImageLib.RefreshGrey16X")
        });

        applicationCommandService.RegisterCommand(new CommandApplicationCommand("GIT Pull", PullAsyncCommand)
        {
            IconObservable = Application.Current!.GetResourceObservable("Entypo+.ArrowLongDownWhite")
        });

        applicationCommandService.RegisterCommand(new CommandApplicationCommand("GIT Push", PushAsyncCommand)
        {
            IconObservable = Application.Current!.GetResourceObservable("Entypo+.ArrowLongUpWhite")
        });

        applicationCommandService.RegisterCommand(
            new CommandApplicationCommand("GIT Create Branch", CreateBranchDialogAsyncCommand)
            {
                IconObservable = Application.Current!.GetResourceObservable("BoxIcons.RegularGitBranch")
            });
    }

    public ObservableCollection<GitRepositoryModel> Repositories { get; } = new();

    public GitRepositoryModel? ActiveRepository
    {
        get => _activeRepository;
        set => SetProperty(ref _activeRepository, value);
    }

    public string CommitMessage
    {
        get => _commitMessage;
        set => SetProperty(ref _commitMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public RelayCommand InitializeRepositoryCommand { get; }
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

    private async Task RefreshAsync()
    {
        InitializeRepositoryCommand.NotifyCanExecuteChanged();

        await WaitUntilFreeAsync();

        IsLoading = true;

        var removeInstances = Repositories.Where(x => !_projectExplorerService.Projects.Contains(x.Project)).ToArray();
        Repositories.RemoveMany(removeInstances);

        try
        {
            foreach (var project in _projectExplorerService.Projects)
                try
                {
                    var path = Repository.Discover(project.RootFolderPath);

                    if (!string.IsNullOrEmpty(path) && Repository.IsValid(path))
                    {
                        if (Repositories.Any(x => x.Project == project)) continue;
                        Repositories.Add(new GitRepositoryModel(project, new Repository(path)));
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e.Message, e);
                }

            foreach (var repo in Repositories)
            {
                //repo.Refresh(this);
                //TODO Show changes for all repos
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }

        ActiveRepository = Repositories.FirstOrDefault(x => x.Project == _projectExplorerService.ActiveProject);

        ActiveRepository?.Refresh(this);

        IsLoading = false;
    }

    #region Initialize and Clone

    public void InitializeRepository()
    {
        if (_projectExplorerService.ActiveProject == null) return;

        try
        {
            Repository.Init(_projectExplorerService.ActiveProject.RootFolderPath);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    public async Task CloneDialogAsync()
    {
        var url = await _windowService.ShowInputAsync("Clone",
            "Enter the remote URL for the repository you want to clone", MessageBoxIcon.Info,
            null, _mainDockService.GetWindowOwner(this));

        if (url == null) return;

        var folder = await _windowService.ShowFolderSelectAsync("Clone",
            "Select the location for the new repository", MessageBoxIcon.Info, _paths.ProjectsDirectory,
            _mainDockService.GetWindowOwner(this));

        if (folder == null) return;

        folder = Path.Combine(folder, Path.GetFileNameWithoutExtension(url) ?? "");
        Directory.CreateDirectory(folder);

        var result = await CloneRepositoryAsync(url, folder);

        if (!result) return;

        await Task.Delay(200);

        var startFilePath = await StorageProviderHelper.SelectFilesAsync(_mainDockService.GetWindowOwner(this)!,
            "Open Project from cloned repository",
            folder);

        foreach (var file in startFilePath)
        {
            //var proj = await MainDock.ProjectFiles.LoadProjectAsync(file);
        }
    }

    private async Task<bool> CloneRepositoryAsync(string url, string destination)
    {
        var success = true;

        var cancellationTokenSource = new CancellationTokenSource();

        var key = _applicationStateService.AddState("Cloning " + Path.GetFileName(url) + "...", AppState.Loading,
            () => cancellationTokenSource.Cancel());

        try
        {
            await Task.Run(() =>
            {
                var options = new CloneOptions
                {
                    FetchOptions =
                    {
                        CredentialsProvider = (crUrl, usernameFromUrl, types) =>
                            GetCredentialsAsync(crUrl, usernameFromUrl, types, cancellationTokenSource.Token).Result
                    },
                    RecurseSubmodules = true
                };
                Repository.Clone(url, destination, options);
            }, cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);

            success = false;
        }

        _applicationStateService.RemoveState(key);

        return success;
    }

    #endregion

    #region General

    private void SetupFetchTimer(double seconds)
    {
        if (_timer != null && Math.Abs(_timer.Interval.TotalSeconds - seconds) < 1) return;
        _timer?.Stop();
        _timer = new DispatcherTimer(new TimeSpan(0, 0, (int)seconds), DispatcherPriority.Normal, FetchTimerCallback);
        _timer.Start();
    }

    private void FetchTimerCallback(object? sender, EventArgs args)
    {
        if (_settingsService.GetSettingValue<bool>("SourceControl_AutoFetchEnable")) _ = FetchAsync();
    }

    private void SetupPollTimer(double seconds)
    {
        if (_timer != null && Math.Abs(_timer.Interval.TotalSeconds - seconds) < 1) return;
        _timer?.Stop();
        _timer = new DispatcherTimer(new TimeSpan(0, 0, (int)seconds), DispatcherPriority.Normal, PollTimerCallback);
        _timer.Start();
    }

    private void PollTimerCallback(object? sender, EventArgs args)
    {
        if (_settingsService.GetSettingValue<bool>("SourceControl_PollChangesEnable")) _ = RefreshAsync();
    }

    public void ViewInProjectExplorer(IProjectEntry entry)
    {
        _mainDockService.Show(_projectExplorerService);
        _projectExplorerService.ExpandToRoot(entry);
        _projectExplorerService.ClearSelection();
        _projectExplorerService.AddToSelection(entry);
    }

    #endregion

    #region Branches & Remotes

    public void ChangeBranch(Branch? branch)
    {
        if (ActiveRepository?.Repository is not { } repository || branch == null) return;

        try
        {
            if (branch.IsRemote)
            {
                var remoteBranch = branch;
                var branchName = branch.FriendlyName.Split("/");

                if (repository.Branches[branchName[1]] is { } localB)
                {
                    branch = localB;
                }
                else
                {
                    branch = repository.CreateBranch(branchName[1], branch.Tip);
                    branch = repository.Branches.Update(branch,
                        b => b.TrackedBranch = remoteBranch.CanonicalName);
                }
            }

            Commands.Checkout(repository, branch);

            _ = RefreshAsync();

            _logger.Log("Switched to branch '" + branch.FriendlyName + "'", true, Brushes.Green);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    private async Task CreateBranchDialogAsync()
    {
        var newBranchName = await _windowService.ShowInputAsync("Create Branch",
            "Please enter a name for the new branch", MessageBoxIcon.Info, null, _mainDockService.GetWindowOwner(this));
        if (newBranchName != null) CreateBranch(newBranchName);
    }

    private Branch? CreateBranch(string name, bool checkout = true)
    {
        if (ActiveRepository?.Repository is not { } repository) return null;

        try
        {
            var newBranch = repository.CreateBranch(name);

            if (checkout) Commands.Checkout(repository, newBranch);

            _ = RefreshAsync();

            return newBranch;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return null;
        }
    }

    private async Task DeleteBranchDialogAsync()
    {
        if (ActiveRepository?.Repository is not { } repository) return;

        var selectedBranchName = await _windowService.ShowInputSelectAsync("Delete Branch",
            "Select the branch you want to delete", MessageBoxIcon.Info,
            repository.Branches.Select(x => x.FriendlyName), repository.Branches.LastOrDefault()?.FriendlyName,
            _mainDockService.GetWindowOwner(this)) as string;

        if (selectedBranchName == null) return;

        var deleteBranch = repository.Branches
            .FirstOrDefault(x => x.FriendlyName == selectedBranchName);

        if (deleteBranch != null)
        {
            await DeleteBranchAsync(deleteBranch);
            _ = RefreshAsync();
        }
    }

    private async Task<bool> DeleteBranchAsync(Branch branch)
    {
        if (ActiveRepository?.Repository is not { } repository) return false;

        try
        {
            repository.Branches.Remove(branch);
            if (branch.IsRemote)
            {
                await WaitUntilFreeAsync();

                IsLoading = true;

                await Task.Run(() =>
                {
                    var remote = repository.Network.Remotes[branch.RemoteName];
                    var pushRefSpec = $"+:refs/heads/{branch.FriendlyName.Split('/')[1]}";
                    var options = new PushOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                            GetCredentialsAsync(url, usernameFromUrl, types).Result
                    };
                    repository.Network.Push(remote, pushRefSpec, options);
                });
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

    private async Task MergeBranchDialogAsync()
    {
        if (ActiveRepository?.Repository is not { } repository) return;

        var selectedBranchName = await _windowService.ShowInputSelectAsync("Merge Branch",
            "Select the branch to merge from", MessageBoxIcon.Info,
            repository.Branches.Select(x => x.FriendlyName), repository.Branches.LastOrDefault()?.FriendlyName,
            _mainDockService.GetWindowOwner(this)) as string;

        if (selectedBranchName == null) return;

        var mergeBranch = repository.Branches
            .FirstOrDefault(x => x.FriendlyName == selectedBranchName);

        if (mergeBranch != null)
            await MergeBranchAsync(mergeBranch);
    }

    private async Task MergeBranchAsync(Branch source)
    {
        if (ActiveRepository?.Repository is not { } repository) return;

        try
        {
            var options = new MergeOptions();
            var result = repository.Merge(source.Tip, await GetSignatureAsync(repository), options);
            if (result != null) PublishMergeResult(result);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    private async Task<bool> PublishBranchDialogAsync()
    {
        if (ActiveRepository?.Repository is not { } repository) return false;

        IsLoading = true;

        bool success;

        try
        {
            if (repository.Head.IsTracking) return true;

            var result = await _windowService.ShowYesNoAsync("Info",
                $"The branch {repository.Head.FriendlyName} has no upstream branch. Would you like to publish this branch?",
                MessageBoxIcon.Info, _mainDockService.GetWindowOwner(this));

            if (result is MessageBoxStatus.Yes)
            {
                repository.Branches.Update(repository.Head,
                    b => b.Remote = repository.Network.Remotes.First().Name,
                    b => b.UpstreamBranch = repository.Head.CanonicalName);

                await Task.Run(() =>
                {
                    repository.Network.Push(repository.Head, new PushOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                            GetCredentialsAsync(url, usernameFromUrl, types).Result
                    });
                });

                _windowService.ShowNotification("Git Info",
                    $"Branch {repository.Head.FriendlyName} published successfully!", NotificationType.Success);

                success = true;
            }

            success = false;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            success = false;
        }

        IsLoading = false;

        return success;
    }

    private async Task<bool> AddRemoteDialogAsync()
    {
        if (ActiveRepository?.Repository is not { } repository) return false;

        if (!repository.Head.IsTracking)
        {
            var url = await _windowService.ShowInputAsync("Add Remote", "Please enter the repository URL",
                MessageBoxIcon.Info,
                null, _mainDockService.GetWindowOwner(this));
            if (url == null) return false;

            var remoteName = await _windowService.ShowInputAsync("Add Remote",
                "Please enter a name for the remote. If this is the first remote you can leave the name as origin.",
                MessageBoxIcon.Info,
                "origin", _mainDockService.GetWindowOwner(this));
            if (remoteName == null) return false;

            return AddRemote(url, remoteName);
        }

        return false;
    }

    private bool AddRemote(string url, string name)
    {
        if (ActiveRepository?.Repository is not { } repository) return false;

        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(name)) return false;

        try
        {
            var remote = repository.Network.Remotes.Add(name, url);
            if (remote != null) return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }

        return false;
    }

    private async Task DeleteRemoteDialogAsync()
    {
        if (ActiveRepository?.Repository is not { } repository) return;

        if (await _windowService.ShowInputSelectAsync("Delete Remote",
                "Select the remote you want to delete", MessageBoxIcon.Info,
                repository.Network.Remotes.Select(x => x.Name), repository.Network.Remotes.LastOrDefault()?.Name,
                _mainDockService.GetWindowOwner(this)) is string selectedRemoteName)
            DeleteRemote(selectedRemoteName);
    }

    private bool DeleteRemote(string name)
    {
        if (ActiveRepository?.Repository is not { } repository) return false;

        if (string.IsNullOrEmpty(name)) return false;

        try
        {
            repository.Network.Remotes.Remove(name);
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

    private async Task CommitAsync(bool staged)
    {
        if (ActiveRepository?.Repository is not { } repository) return;

        try
        {
            if (!staged) Commands.Stage(repository, "*");

            var author = await GetSignatureAsync(repository);
            var committer = author;
            var commit = repository.Commit(CommitMessage, author, committer);

            _logger.Log($"Commit {commit.Message}", true, Brushes.Green);
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
        if (ActiveRepository?.Repository is not { } repository) return;

        var push = true;
        if (!repository.Network.Remotes.Any())
        {
            var result = await _windowService.ShowYesNoAsync("Warning",
                "This repository does not have a remote. Do you want to add one?", MessageBoxIcon.Warning,
                _mainDockService.GetWindowOwner(this));
            if (result is MessageBoxStatus.Yes)
            {
                if (!await AddRemoteDialogAsync()) return;
            }
            else
            {
                return;
            }
        }

        if (!repository.Head.IsTracking)
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
            //if (pushResult)
            //_windowService.ShowNotification("Success", "Sync finished successfully", NotificationType.Success);
            _ = RefreshAsync();
        }
    }

    private async Task WaitUntilFreeAsync()
    {
        while (IsLoading) await Task.Delay(100);
    }

    private async Task<MergeResult?> PullAsync()
    {
        if (ActiveRepository?.Repository is not { } repository) return null;

        var pullState =
            _applicationStateService.AddState("Pulling from " + repository.Head.RemoteName, AppState.Loading);
        await WaitUntilFreeAsync();
        IsLoading = true;

        //foreach(var terminal in MainDock.Terminals)
        //{
        //    terminal.CloseConnection();
        //}

        var signature = await GetSignatureAsync(repository);

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
                            GetCredentialsAsync(url, usernameFromUrl, types).Result
                    }
                };

                var mergeResult = Commands.Pull(repository, signature, options);

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
            _logger.Log($"Pull Status: {result.Status}", true);
            PublishMergeResult(result);
        }

        return result;
    }

    private void PublishMergeResult(MergeResult result)
    {
        switch (result.Status)
        {
            case MergeStatus.Conflicts:
                _windowService.ShowNotification("Git Warning",
                    "There are merge conflicts. Resolve them before committing.", NotificationType.Warning);
                _mainDockService.Show(this);
                break;

            case MergeStatus.UpToDate:
                _windowService.ShowNotification("Git Info", "Repository up to date");
                break;

            case MergeStatus.FastForward:
                _windowService.ShowNotification("Git Info", "Pulled changes fast forward",
                    NotificationType.Success);
                break;
        }
    }

    private async Task<bool> PushAsync()
    {
        if (ActiveRepository?.Repository is not { } repository) return false;

        if (!repository.Head.IsTracking)
        {
            var success = await PublishBranchDialogAsync();
            if (!success) return false;
        }

        if (ActiveRepository.PushCommits == 0)
        {
            _logger.Log("Nothing to push");
            return true;
        }

        var pullState =
            _applicationStateService.AddState("Pushing to " + repository.Head.RemoteName, AppState.Loading);
        await WaitUntilFreeAsync();
        IsLoading = true;

        var result = await Task.Run(() =>
        {
            try
            {
                var pushOptions = new PushOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) =>
                        GetCredentialsAsync(url, usernameFromUrl, types).Result
                };
                //PUSH                 
                repository.Network.Push(repository.Head, pushOptions);
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
            _windowService.ShowNotification("Git Info", $"Pushed successfully to {repository.Head.FriendlyName}",
                NotificationType.Success);

        return result;
    }

    private async Task FetchAsync()
    {
        if (ActiveRepository?.Repository is not { } repository) return;

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
                        GetCredentialsAsync(url, usernameFromUrl, types).Result
                };

                foreach (var remote in repository.Network.Remotes)
                {
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    Commands.Fetch(repository, remote.Name, refSpecs, options, logMessage);
                }

                ActiveRepository.PullCommits = repository.Head.TrackingDetails.BehindBy ?? 0;
                ActiveRepository.PushCommits = repository.Head.TrackingDetails.AheadBy ?? 0;
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

    private void StageAll()
    {
        if (ActiveRepository?.Repository is not { } repository) return;
        Commands.Stage(repository, "*");
        _ = RefreshAsync();
    }

    private void UnStageAll()
    {
        if (ActiveRepository?.Repository is not { } repository) return;
        Commands.Unstage(repository, "*");
        _ = RefreshAsync();
    }

    public void Stage(string? path)
    {
        if (ActiveRepository?.Repository is not { } repository) return;
        Commands.Stage(repository, path);

        _ = RefreshAsync();
    }

    public void UnStage(string? path)
    {
        if (ActiveRepository?.Repository is not { } repository) return;
        Commands.Unstage(repository, path);

        _ = RefreshAsync();
    }

    public async Task DiscardAsync(string path)
    {
        if (ActiveRepository?.Repository is not { } repository) return;

        var options = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
        repository.CheckoutPaths(repository.Head.FriendlyName, new[] { path }, options);

        if (!Path.IsPathRooted(path)) path = Path.Combine(repository.Info.WorkingDirectory, path);

        var entry = ActiveRepository.Changes
            .FirstOrDefault(x => Path.Combine(repository.Info.WorkingDirectory, x.Status.FilePath) == path);
        if (entry is { Status.State: FileStatus.NewInWorkdir })
        {
            var result = await _windowService.ShowYesNoCancelAsync("Warning",
                $"Are you sure you want to delete {Path.GetFileName(path)}?", MessageBoxIcon.Warning);

            if (result is MessageBoxStatus.Yes)
                try
                {
                    File.Delete(path);
                    if (_projectExplorerService.ActiveProject?.SearchFullPath(Path.Combine(
                            repository.Info.WorkingDirectory,
                            path)) is IProjectFile file)
                        _ = _projectExplorerService.RemoveAsync(file);
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
        }

        await RefreshAsync();
    }

    private async Task DiscardAllAsync(ResetMode mode)
    {
        if (ActiveRepository?.Repository is not { } repository) return;

        try
        {
            await WaitUntilFreeAsync();
            repository.Reset(mode);

            if (mode == ResetMode.Hard)
            {
                var deleteFiles = new List<string>();
                foreach (var item in repository.RetrieveStatus(new StatusOptions()))
                    if (item.State == FileStatus.NewInWorkdir)
                    {
                        var path = Path.Combine(repository.Info.WorkingDirectory, item.FilePath);
                        deleteFiles.Add(path);
                    }

                if (deleteFiles.Any())
                {
                    var result = await _windowService.ShowYesNoCancelAsync("Warning",
                        $"Do you want to delete {deleteFiles.Count} untracked files forever?", MessageBoxIcon.Warning);

                    if (result is MessageBoxStatus.Yes)
                        foreach (var f in deleteFiles)
                        {
                            var projFile = _projectExplorerService.SearchFullPath(f);
                            if (projFile != null) await _projectExplorerService.DeleteAsync(projFile);
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
        if (ActiveRepository?.Repository is not { } repository) return null;

        if (!Path.IsPathRooted(path)) path = Path.Combine(repository.Info.WorkingDirectory, path);

        if (_projectExplorerService.ActiveProject?.SearchFullPath(path) is not IFile file)
            file = _projectExplorerService.GetTemporaryFile(path);

        await _mainDockService.OpenFileAsync(file);
        return file;
    }

    public async Task OpenHeadFileAsync(string path)
    {
        if (ActiveRepository?.Repository is not { } repository) return;

        string commitContent;
        var blob = repository.Head.Tip[path].Target as Blob;

        if (blob == null) throw new NullReferenceException(nameof(blob));

        using (var content = new StreamReader(blob.GetContentStream(), Encoding.UTF8))
        {
            commitContent = await content.ReadToEndAsync();
        }

        var file = _projectExplorerService.GetTemporaryFile(path);

        var evm = await _mainDockService.OpenFileAsync(file);

        if (evm is IEditor editor)
        {
            editor.Title += " (HEAD)";
            editor.IsReadOnly = true;
            editor.CurrentDocument.Text = commitContent;
        }
    }

    public void CompareAndSwitch(string path)
    {
        if (ActiveRepository?.Repository is not { } repository) return;
        Compare(path, true);
    }

    public void Compare(string path, bool switchTab)
    {
        if (ActiveRepository?.Repository is not { } repository) return;
        _ = CompareChangesAsync(repository, path, "Diff: ", 10000, switchTab);
    }

    public void ViewChanges(string path)
    {
        if (ActiveRepository?.Repository is not { } repository) return;
        _ = CompareChangesAsync(repository, path, "Changes: ");
    }

    public Patch? GetPatch(string path, int contextLines)
    {
        if (ActiveRepository?.Repository is not { } repository) return null;

        return repository.Diff.Compare<Patch>(new List<string> { path }, false,
            new ExplicitPathsOptions(),
            new CompareOptions { ContextLines = contextLines });
    }

    private async Task CompareChangesAsync(Repository repository, string path, string titlePrefix, int contextLines = 3,
        bool switchTab = true)
    {
        await WaitUntilFreeAsync();
        try
        {
            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(repository.Info.WorkingDirectory, path.Replace('/', Path.DirectorySeparatorChar));

            var openTab = _mainDockService.SearchView<CompareGitViewModel>()
                .FirstOrDefault(x => x.FullPath == fullPath);
            openTab ??= ContainerLocator.Container.Resolve<CompareGitViewModel>((typeof(string), fullPath));

            openTab.Title = titlePrefix + Path.GetFileName(path);
            openTab.Id = titlePrefix + fullPath;

            _mainDockService.Show(openTab, DockShowLocation.Document);
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

    private async Task MergeAllAsync(string path, MergeMode mode)
    {
        var file = await OpenFileAsync(path);
        if (file == null) return;

        var evm = await _mainDockService.OpenFileAsync(file);
        if (evm is IEditor editor)
        {
            var merges = MergeService.GetMerges(editor.CurrentDocument);
            merges.Reverse(); //Reverse to avoid mistakes with wrong index
            foreach (var merge in merges) MergeService.Merge(editor.CurrentDocument, merge, mode);
        }
    }

    #endregion

    #region Login

    public Task<bool> LoginGithubAsync()
    {
        return LoginDialogAsync(ContainerLocator.Container.Resolve<GithubLoginProvider>());
    }

    private async Task<bool> LoginDialogAsync(ILoginProvider loginProvider)
    {
        var vm = new AuthenticateGitViewModel(loginProvider);
        await Dispatcher.UIThread.InvokeAsync(() => _windowService.ShowDialogAsync(new AuthenticateGitView
        {
            DataContext = vm
        }, _mainDockService.GetWindowOwner(this)));
        return vm.Success;
    }

    private async Task<Credentials> GetCredentialsAsync(string url, string usernameFromUrl,
        SupportedCredentialTypes types, CancellationToken cancellationToken = default)
    {
        if (types.HasFlag(SupportedCredentialTypes.UsernamePassword))
        {
            var ub = new Uri(url);

            var username = _settingsService.GetSettingValue<string>(SourceControlModule.GitHubAccountNameKey);

            var store = CredentialManager.Create("oneware");

            if (!string.IsNullOrWhiteSpace(username))
            {
                var key = $"{ub.Scheme}://{ub.Host}";
                var cred = store.Get(key, username);

                if (cred != null)
                    return new UsernamePasswordCredentials
                    {
                        Username = cred.Account,
                        Password = cred.Password
                    };
            }

            var loginResult = false;

            if (_loginProviders.TryGetValue(ub.Host, out var loginProvider))
                loginResult = await LoginDialogAsync(loginProvider);

            if (cancellationToken.IsCancellationRequested) return new DefaultCredentials();

            if (loginResult) return await GetCredentialsAsync(url, usernameFromUrl, types, cancellationToken);
        }

        return new DefaultCredentials();
    }

    #endregion

    #region Identity

    private async Task<Signature?> GetSignatureAsync(Repository repository)
    {
        var author = repository.Config.BuildSignature(DateTimeOffset.Now);

        if (author == null)
        {
            var identity = await SetUserIdentityAsync(true);

            author = new Signature(identity, DateTime.Now);
        }

        return author;
    }

    private async Task<Identity?> GetIdentityManualAsync(Repository repository)
    {
        var author = repository.Config.BuildSignature(DateTimeOffset.Now);

        var name = await _windowService.ShowInputAsync("Info", "Please enter a name to sign your changes",
            MessageBoxIcon.Info, author?.Name);
        if (name == null) return null;

        var email = await _windowService.ShowInputAsync("Info",
            "Please enter a valid email adress to sign your changes", MessageBoxIcon.Info, author?.Email);
        if (email == null) return null;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            _logger.Error("Username and/or email can't be empty", null, false, true);
            return null;
        }

        return new Identity(name, email);
    }

    private async Task<Identity?> SetUserIdentityAsync(bool dialog)
    {
        if (ActiveRepository?.Repository is not { } repository) return null;

        var identity = await GetIdentityManualAsync(repository);

        if (identity == null) return null;

        var result = dialog
            ? await _windowService.ShowYesNoAsync("Info",
                "Do you want to save this information in your global git configuration so that you do not have to enter them again next time?",
                MessageBoxIcon.Info)
            : MessageBoxStatus.Yes;

        if (result is MessageBoxStatus.Yes)
        {
            if (!repository.Config.HasConfig(ConfigurationLevel.Global))
            {
                try
                {
                    var globalConfig =
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".gitconfig");
                    await File.WriteAllTextAsync(globalConfig,
                        $"[user]\n\tname = {identity.Name}\n\temail = {identity.Email}\n", Encoding.UTF8);
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
            }
            else
            {
                repository.Config.Set("user.name", identity.Name, ConfigurationLevel.Global);
                repository.Config.Set("user.email", identity.Email, ConfigurationLevel.Global);
            }
        }

        return identity;
    }

    #endregion
}