using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
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

public class SourceControlViewModel : ExtendedTool, IDisposable
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

    private DispatcherTimer? _fetchTimer;
    private DispatcherTimer? _pollTimer;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly CompositeDisposable _subscriptions = new();
    private bool _disposed;

    public SourceControlViewModel(ILogger logger, ISettingsService settingsService,
        IApplicationStateService applicationStateService,
        IFileIconService fileIconService,
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
        FileIconService = fileIconService;
        
        Id = "SourceControl";

        InitializeRepositoryCommand =
            new AsyncRelayCommand(InitializeRepositoryAsync, () => !IsLoading && ActiveRepository == null && _projectExplorerService.ActiveProject != null);
        RefreshAsyncCommand = new AsyncRelayCommand(RefreshAsync);
        CloneDialogAsyncCommand = new AsyncRelayCommand(CloneDialogAsync);
        SyncAsyncCommand = new AsyncRelayCommand(SyncAsync, CanUseRepository);
        PullAsyncCommand = new AsyncRelayCommand(PullAsync, CanUseRepository);
        PushAsyncCommand = new AsyncRelayCommand(PushAsync, CanUseRepository);
        FetchAsyncCommand = new AsyncRelayCommand(FetchAsync, CanUseRepository);
        CommitAsyncCommand = new AsyncRelayCommand<bool>(CommitAsync, staged => CanUseRepository() &&
            !string.IsNullOrWhiteSpace(CommitMessage) && ActiveRepository!.MergeChanges.Count == 0 &&
            (ActiveRepository.StagedChanges.Count > 0 || (!staged && ActiveRepository.Changes.Count > 0) ||
             ActiveRepository.Repository.Info.CurrentOperation == CurrentOperation.Merge));
        DiscardAllAsyncCommand = new AsyncRelayCommand<ResetMode>(DiscardAllAsync, _ => CanUseRepository());
        StageAllCommand = new RelayCommand(StageAll, CanUseRepository);
        UnStageAllCommand = new RelayCommand(UnStageAll, CanUseRepository);
        StageCommand = new RelayCommand<string>(Stage, path => CanUseRepository() && !string.IsNullOrWhiteSpace(path));
        UnStageCommand = new RelayCommand<string>(UnStage, path => CanUseRepository() && !string.IsNullOrWhiteSpace(path));
        CreateBranchDialogAsyncCommand = new AsyncRelayCommand(CreateBranchDialogAsync, CanUseRepository);
        MergeBranchDialogAsyncCommand = new AsyncRelayCommand(MergeBranchDialogAsync, CanUseRepository);
        DeleteBranchDialogAsyncCommand = new AsyncRelayCommand(DeleteBranchDialogAsync, CanUseRepository);
        AddRemoteDialogAsyncCommand = new AsyncRelayCommand(AddRemoteDialogAsync, CanUseRepository);
        DeleteRemoteDialogAsyncCommand = new AsyncRelayCommand(DeleteRemoteDialogAsync, CanUseRepository);
        SetUserIdentityAsyncCommand = new AsyncRelayCommand<bool>(SetUserIdentityAsync, _ => CanUseRepository());

        _subscriptions.Add(settingsService.GetSettingObservable<double>("SourceControl_AutoFetchDelay")
            .Subscribe(SetupFetchTimer));

        _subscriptions.Add(settingsService.GetSettingObservable<double>("SourceControl_PollChangesDelay")
            .Subscribe(SetupPollTimer));

        _subscriptions.Add(projectExplorerService
            .WhenValueChanged(x => x.ActiveProject)
            .Subscribe(project => { _ = RefreshAsync(); }));

        _loginProviders.Add("github.com", ContainerLocator.Container.Resolve<GithubLoginProvider>());

        applicationCommandService.RegisterCommand(new CommandApplicationCommand("GIT Sync", SyncAsyncCommand)
        {
            Icon = new IconModel("VsImageLib.RefreshGrey16X")
        });

        applicationCommandService.RegisterCommand(new CommandApplicationCommand("GIT Pull", PullAsyncCommand)
        {
            Icon = new IconModel("Entypo+.ArrowLongDownWhite")
        });

        applicationCommandService.RegisterCommand(new CommandApplicationCommand("GIT Push", PushAsyncCommand)
        {
            Icon = new IconModel("Entypo+.ArrowLongUpWhite")
        });

        applicationCommandService.RegisterCommand(
            new CommandApplicationCommand("GIT Create Branch", CreateBranchDialogAsyncCommand)
            {
                Icon = new IconModel("BoxIcons.RegularGitBranch")
            });
    }

    public IFileIconService FileIconService { get; set; }

    public ObservableCollection<GitRepositoryModel> Repositories { get; } = new();

    public GitRepositoryModel? ActiveRepository
    {
        get => _activeRepository;
        set
        {
            if (SetProperty(ref _activeRepository, value)) NotifyCommands();
        }
    }

    public string CommitMessage
    {
        get => _commitMessage;
        set
        {
            if (SetProperty(ref _commitMessage, value)) CommitAsyncCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value)) NotifyCommands();
        }
    }

    public AsyncRelayCommand InitializeRepositoryCommand { get; }
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

    public override void InitializeContent()
    {
        base.InitializeContent();
        
        Title = "Source Control";
    }

    private async Task RefreshAsync()
    {
        await _operationGate.WaitAsync();
        try
        {
            if (_disposed) return;
            IsLoading = true;
            var removeInstances = Repositories.Where(x => !_projectExplorerService.Projects.Contains(x.Project)).ToArray();
            foreach (var removed in removeInstances)
            {
                Repositories.Remove(removed);
                removed.Dispose();
            }

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

            ActiveRepository = Repositories.FirstOrDefault(x => x.Project == _projectExplorerService.ActiveProject);
            if (ActiveRepository != null) await ActiveRepository.RefreshAsync(this);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
        finally
        {
            EndOperation();
        }
    }

    private bool CanUseRepository() => !_disposed && !IsLoading && ActiveRepository != null;

    private void NotifyCommands()
    {
        InitializeRepositoryCommand.NotifyCanExecuteChanged();
        SyncAsyncCommand.NotifyCanExecuteChanged();
        PullAsyncCommand.NotifyCanExecuteChanged();
        PushAsyncCommand.NotifyCanExecuteChanged();
        FetchAsyncCommand.NotifyCanExecuteChanged();
        CommitAsyncCommand.NotifyCanExecuteChanged();
        DiscardAllAsyncCommand.NotifyCanExecuteChanged();
        StageAllCommand.NotifyCanExecuteChanged();
        UnStageAllCommand.NotifyCanExecuteChanged();
        StageCommand.NotifyCanExecuteChanged();
        UnStageCommand.NotifyCanExecuteChanged();
        CreateBranchDialogAsyncCommand.NotifyCanExecuteChanged();
        MergeBranchDialogAsyncCommand.NotifyCanExecuteChanged();
        DeleteBranchDialogAsyncCommand.NotifyCanExecuteChanged();
        AddRemoteDialogAsyncCommand.NotifyCanExecuteChanged();
        DeleteRemoteDialogAsyncCommand.NotifyCanExecuteChanged();
        SetUserIdentityAsyncCommand.NotifyCanExecuteChanged();
    }

    private async Task RunRepositoryOperationAsync(Func<Repository, Task> operation)
    {
        var model = ActiveRepository;
        if (model == null || _disposed) return;
        await _operationGate.WaitAsync();
        var started = false;
        try
        {
            // Never run a queued command against a different or already closed project.
            if (_disposed || ActiveRepository != model || !Repositories.Contains(model) ||
                _projectExplorerService.ActiveProject != model.Project) return;
            IsLoading = true;
            started = true;
            await operation(model.Repository);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
        finally
        {
            try
            {
                if (started && !_disposed && Repositories.Contains(model)) await model.RefreshAsync(this);
            }
            finally
            {
                EndOperation();
            }
        }
    }

    private void EndOperation()
    {
        try
        {
            if (_disposed)
            {
                foreach (var repository in Repositories) repository.Dispose();
                Repositories.Clear();
                ActiveRepository = null;
            }
            IsLoading = false;
            NotifyCommands();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fetchTimer?.Stop();
        _pollTimer?.Stop();
        _subscriptions.Dispose();
        // An in-flight operation owns the native handles until it finishes.
        if (_operationGate.Wait(0)) EndOperation();
    }

    #region Initialize and Clone

    public async Task InitializeRepositoryAsync()
    {
        var project = _projectExplorerService.ActiveProject;
        if (project == null || _disposed) return;
        await _operationGate.WaitAsync();
        try
        {
            if (_disposed) return;
            IsLoading = true;
            await Task.Run(() => Repository.Init(project.RootFolderPath));
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
        finally
        {
            EndOperation();
        }
        await RefreshAsync();
    }

    public async Task CloneDialogAsync()
    {
        var url = await _windowService.ShowInputAsync("Clone",
            "Enter the remote URL for the repository you want to clone", MessageBoxIcon.Info,
            null, _mainDockService.GetWindowOwner(this));

        if (string.IsNullOrWhiteSpace(url)) return;
        url = url.Trim();

        string name;
        try
        {
            name = GitOperations.GetCloneDirectoryName(url);
        }
        catch (ArgumentException e)
        {
            _windowService.ShowNotification("Clone", e.Message, NotificationType.Warning);
            return;
        }

        var folder = await _windowService.ShowFolderSelectAsync("Clone",
            "Select the location for the new repository", MessageBoxIcon.Info, _paths.ProjectsDirectory,
            _mainDockService.GetWindowOwner(this));

        if (folder == null) return;

        folder = Path.Combine(folder, name);
        try
        {
            if (File.Exists(folder) || (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()))
            {
                _windowService.ShowNotification("Clone", "The destination already exists and is not empty. Choose another location.", NotificationType.Warning);
                return;
            }
            if (!await CloneRepositoryAsync(url, folder)) return;
            var manager = ContainerLocator.Container.Resolve<IProjectManagerService>().GetManager("Folder");
            if (manager != null && await _windowService.ShowYesNoAsync("Clone Complete",
                    "Open the cloned repository as a folder project?", MessageBoxIcon.Info,
                    _mainDockService.GetWindowOwner(this)) == MessageBoxStatus.Yes)
            {
                await _projectExplorerService.LoadProjectAsync(folder, manager);
                _mainDockService.Show(_projectExplorerService);
                await RefreshAsync();
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    private async Task<bool> CloneRepositoryAsync(string url, string destination)
    {
        using var cancellationTokenSource = new CancellationTokenSource();

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
                            GetCredentialsAsync(crUrl, usernameFromUrl, types, cancellationTokenSource.Token).GetAwaiter().GetResult(),
                        OnTransferProgress = _ => !cancellationTokenSource.IsCancellationRequested
                    },
                    RecurseSubmodules = true
                };
                Repository.Clone(url, destination, options);
            }, cancellationTokenSource.Token);
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
            return true;
        }
        catch (Exception) when (cancellationTokenSource.IsCancellationRequested)
        {
            _windowService.ShowNotification("Clone", "Cloning cancelled. Any downloaded files have been left in the destination folder.");
            return false;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }
        finally
        {
            _applicationStateService.RemoveState(key);
        }
    }

    #endregion

    #region General

    private void SetupFetchTimer(double seconds)
    {
        _fetchTimer?.Stop();
        if (_disposed || !double.IsFinite(seconds) || seconds <= 0) return;
        _fetchTimer = new DispatcherTimer(TimeSpan.FromSeconds(seconds), DispatcherPriority.Normal, FetchTimerCallback);
        _fetchTimer.Start();
    }

    private void FetchTimerCallback(object? sender, EventArgs args)
    {
        if (CanUseRepository() && _settingsService.GetSettingValue<bool>("SourceControl_AutoFetchEnable"))
            _ = FetchAsync(false);
    }

    private void SetupPollTimer(double seconds)
    {
        _pollTimer?.Stop();
        if (_disposed || !double.IsFinite(seconds) || seconds <= 0) return;
        _pollTimer = new DispatcherTimer(TimeSpan.FromSeconds(seconds), DispatcherPriority.Normal, PollTimerCallback);
        _pollTimer.Start();
    }

    private void PollTimerCallback(object? sender, EventArgs args)
    {
        if (!_disposed && !IsLoading && _settingsService.GetSettingValue<bool>("SourceControl_PollChangesEnable"))
            _ = RefreshAsync();
    }

    public void ViewInProjectExplorer(string fullPath)
    {
        _mainDockService.Show(_projectExplorerService);

        var file = _projectExplorerService.GetEntryFromFullPath(fullPath);
        
        if(file == null) return;
        
        _projectExplorerService.ExpandToRoot(file);
        _projectExplorerService.ClearSelection();
        _projectExplorerService.AddToSelection(file);
    }

    #endregion

    #region Branches & Remotes

    public void ChangeBranch(Branch? branch)
    {
        if (branch == null || !CanUseRepository()) return;
        _ = RunRepositoryOperationAsync(async repository =>
        {
            var checkedOut = await Task.Run(() => GitOperations.CheckoutBranch(repository, branch));
            _logger.Log("Switched to branch '" + checkedOut.FriendlyName + "'", true, Brushes.Green);
        });
    }

    private Task CreateBranchDialogAsync()
    {
        return RunRepositoryOperationAsync(async repository =>
        {
            var name = await _windowService.ShowInputAsync("Create Branch",
                "Please enter a name for the new branch", MessageBoxIcon.Info, null, _mainDockService.GetWindowOwner(this));
            if (string.IsNullOrWhiteSpace(name)) return;
            await Task.Run(() => Commands.Checkout(repository, repository.CreateBranch(name.Trim())));
        });
    }

    private Task DeleteBranchDialogAsync()
    {
        return RunRepositoryOperationAsync(async repository =>
        {
            var names = repository.Branches.Where(x => !x.IsCurrentRepositoryHead)
                .Select(x => x.FriendlyName).OrderBy(x => x).ToArray();
            var selected = await _windowService.ShowInputSelectAsync("Delete Branch",
                "Select the branch you want to delete", MessageBoxIcon.Info, names, names.FirstOrDefault(),
                _mainDockService.GetWindowOwner(this)) as string;
            if (selected == null || repository.Branches[selected] is not { } branch) return;
            var warning = branch.IsRemote
                ? $"Delete remote branch '{selected}' from the server? This affects everyone using this repository."
                : $"Delete local branch '{selected}'? Commits that have not been merged may become unreachable.";
            if (await _windowService.ShowYesNoAsync("Delete Branch", warning, MessageBoxIcon.Warning,
                    _mainDockService.GetWindowOwner(this)) != MessageBoxStatus.Yes) return;

            if (branch.IsRemote)
            {
                await Task.Run(() => GitOperations.DeleteRemoteBranch(repository, branch, CreatePushOptions()));
            }
            else repository.Branches.Remove(branch);
        });
    }

    private Task MergeBranchDialogAsync()
    {
        return RunRepositoryOperationAsync(async repository =>
        {
            if (!CanIntegrate(repository)) return;
            var names = repository.Branches.Where(x => !x.IsCurrentRepositoryHead).Select(x => x.FriendlyName).ToArray();
            var selected = await _windowService.ShowInputSelectAsync("Merge Branch",
                "Select the branch to merge from", MessageBoxIcon.Info, names, names.FirstOrDefault(),
                _mainDockService.GetWindowOwner(this)) as string;
            if (selected == null || repository.Branches[selected] is not { } source) return;
            var signature = await GetSignatureAsync(repository);
            if (signature == null) return;
            var result = await Task.Run(() => repository.Merge(source.Tip, signature, new MergeOptions()));
            PublishMergeResult(result);
        });
    }

    private async Task<bool> PublishBranchDialogAsync(Repository repository)
    {
        if (repository.Head.IsTracking) return true;
        if (!CanPush(repository)) return false;
        if (!repository.Network.Remotes.Any() && !await AddRemoteAsync(repository)) return false;

        if (await _windowService.ShowYesNoAsync("Publish Branch",
                $"The branch {repository.Head.FriendlyName} has no upstream branch. Would you like to publish it?",
                MessageBoxIcon.Info, _mainDockService.GetWindowOwner(this)) != MessageBoxStatus.Yes) return false;

        var remotes = repository.Network.Remotes.Select(x => x.Name).ToArray();
        var remoteName = remotes.Length == 1 ? remotes[0] :
            await _windowService.ShowInputSelectAsync("Publish Branch", "Select the destination remote",
                MessageBoxIcon.Info, remotes, remotes.Contains("origin") ? "origin" : remotes[0],
                _mainDockService.GetWindowOwner(this)) as string;
        if (remoteName == null) return false;

        var head = await Task.Run(() => GitOperations.PublishBranch(repository, remoteName, CreatePushOptions()));
        _windowService.ShowNotification("Git Info", $"Branch {head.FriendlyName} published successfully!", NotificationType.Success);
        return true;
    }

    private Task AddRemoteDialogAsync() => RunRepositoryOperationAsync(async repository => { await AddRemoteAsync(repository); });

    private async Task<bool> AddRemoteAsync(Repository repository)
    {
        var url = await _windowService.ShowInputAsync("Add Remote", "Please enter the repository URL",
            MessageBoxIcon.Info, null, _mainDockService.GetWindowOwner(this));
        if (string.IsNullOrWhiteSpace(url)) return false;
        var name = await _windowService.ShowInputAsync("Add Remote", "Please enter a name for the remote",
            MessageBoxIcon.Info, repository.Network.Remotes["origin"] == null ? "origin" : null,
            _mainDockService.GetWindowOwner(this));
        if (string.IsNullOrWhiteSpace(name)) return false;
        repository.Network.Remotes.Add(name.Trim(), url.Trim());
        return true;
    }

    private Task DeleteRemoteDialogAsync()
    {
        return RunRepositoryOperationAsync(async repository =>
        {
            if (await _windowService.ShowInputSelectAsync("Delete Remote",
                "Select the remote you want to delete", MessageBoxIcon.Info,
                repository.Network.Remotes.Select(x => x.Name), repository.Network.Remotes.LastOrDefault()?.Name,
                _mainDockService.GetWindowOwner(this)) is not string name) return;
            if (await _windowService.ShowYesNoAsync("Delete Remote", $"Remove remote '{name}' from this repository?",
                    MessageBoxIcon.Warning, _mainDockService.GetWindowOwner(this)) == MessageBoxStatus.Yes)
                repository.Network.Remotes.Remove(name);
        });
    }

    #endregion

    #region Commit & Sync

    private Task CommitAsync(bool staged)
    {
        var message = CommitMessage;
        return RunRepositoryOperationAsync(async repository =>
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            var author = await GetSignatureAsync(repository);
            if (author == null) return;
            var commit = await Task.Run(() => GitOperations.Commit(repository, message, author, staged));
            _logger.Log($"Commit {commit.Message}", true, Brushes.Green);
            if (CommitMessage == message) CommitMessage = "";
        });
    }

    public Task SyncAsync()
    {
        return RunRepositoryOperationAsync(async repository =>
        {
            if (!CanPush(repository) || !CanIntegrate(repository)) return;
            if (!repository.Head.IsTracking)
            {
                await PublishBranchDialogAsync(repository);
                return;
            }
            var result = await PullCoreAsync(repository);
            if (result != null && result.Status != MergeStatus.Conflicts)
                await PushCoreAsync(repository);
        });
    }

    private bool CanIntegrate(Repository repository)
    {
        if (repository.Index.Conflicts.Any() || repository.Info.CurrentOperation != CurrentOperation.None)
        {
            _windowService.ShowNotification("Git Warning", "Finish the current merge or resolve conflicts before pulling or merging.", NotificationType.Warning);
            return false;
        }
        return true;
    }

    private bool CanPush(Repository repository)
    {
        if (repository.Info.IsHeadDetached || repository.Head.Tip == null)
        {
            _windowService.ShowNotification("Git Warning", "Check out a branch with at least one commit before pushing.", NotificationType.Warning);
            return false;
        }
        return true;
    }

    private Task PullAsync() => RunRepositoryOperationAsync(async repository => { await PullCoreAsync(repository); });

    private async Task<MergeResult?> PullCoreAsync(Repository repository)
    {
        if (!CanIntegrate(repository)) return null;
        if (!repository.Head.IsTracking)
        {
            _windowService.ShowNotification("Git Info", "This branch has no upstream. Publish it with Push, or check out a remote branch.");
            return null;
        }
        var signature = await GetSignatureAsync(repository);
        if (signature == null) return null;
        var state = _applicationStateService.AddState("Pulling from " + repository.Head.RemoteName, AppState.Loading);
        try
        {
            var result = await Task.Run(() => Commands.Pull(repository, signature,
                new PullOptions { FetchOptions = CreateFetchOptions() }));
            _logger.Log($"Pull Status: {result.Status}", true);
            PublishMergeResult(result);
            return result;
        }
        finally
        {
            _applicationStateService.RemoveState(state);
        }
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

    private Task PushAsync() => RunRepositoryOperationAsync(PushCoreAsync);

    private async Task PushCoreAsync(Repository repository)
    {
        if (!CanPush(repository)) return;
        if (!repository.Head.IsTracking)
        {
            await PublishBranchDialogAsync(repository);
            return;
        }
        // Do not skip a push based on stale ahead/behind counts from the UI.
        var state = _applicationStateService.AddState("Pushing to " + repository.Head.RemoteName, AppState.Loading);
        try
        {
            await Task.Run(() => repository.Network.Push(repository.Head, CreatePushOptions()));
            _windowService.ShowNotification("Git Info", $"Pushed successfully to {repository.Head.FriendlyName}",
                NotificationType.Success);
        }
        finally
        {
            _applicationStateService.RemoveState(state);
        }
    }

    private Task FetchAsync() => FetchAsync(true);

    private Task FetchAsync(bool interactive)
    {
        return RunRepositoryOperationAsync(async repository =>
        {
            foreach (var remote in repository.Network.Remotes)
            {
                try
                {
                    await Task.Run(() => Commands.Fetch(repository, remote.Name,
                        remote.FetchRefSpecs.Select(x => x.Specification), CreateFetchOptions(interactive), ""));
                }
                catch (Exception e)
                {
                    if (interactive) _logger.Error(e.Message, e);
                    else _logger.LogDebug(e, "Automatic Git fetch failed for remote {Remote}", remote.Name);
                }
            }
        });
    }

    // LibGit2Sharp requires synchronous callbacks. These are only invoked on worker threads.
    private FetchOptions CreateFetchOptions(bool interactive = true) => new()
    {
        Prune = true,
        CredentialsProvider = (url, username, types) =>
            GetCredentialsAsync(url, username, types, interactive: interactive).GetAwaiter().GetResult()
    };

    private PushOptions CreatePushOptions() => new()
    {
        CredentialsProvider = (url, username, types) => GetCredentialsAsync(url, username, types).GetAwaiter().GetResult(),
        OnPushStatusError = error => throw new InvalidOperationException($"Push rejected for {error.Reference}: {error.Message}")
    };

    #endregion

    #region Stage & Discard

    private void StageAll()
    {
        if (!CanUseRepository()) return;
        _ = RunRepositoryOperationAsync(repository => Task.Run(() => Commands.Stage(repository, "*")));
    }

    private void UnStageAll()
    {
        if (!CanUseRepository()) return;
        _ = RunRepositoryOperationAsync(repository => Task.Run(() => Commands.Unstage(repository, "*")));
    }

    public void Stage(string? path)
    {
        if (!CanUseRepository() || string.IsNullOrWhiteSpace(path)) return;
        _ = RunRepositoryOperationAsync(repository => Task.Run(() =>
            Commands.Stage(repository, GitOperations.GetRelativePath(repository, path))));
    }

    public void UnStage(string? path)
    {
        if (!CanUseRepository() || string.IsNullOrWhiteSpace(path)) return;
        _ = RunRepositoryOperationAsync(repository => Task.Run(() =>
            Commands.Unstage(repository, GitOperations.GetRelativePath(repository, path))));
    }

    public Task DiscardAsync(string path)
    {
        return RunRepositoryOperationAsync(async repository =>
        {
            var relativePath = GitOperations.GetRelativePath(repository, path);
            var untracked = repository.RetrieveStatus(relativePath).HasFlag(FileStatus.NewInWorkdir);
            var message = untracked
                ? $"Permanently delete untracked file '{relativePath}'? This cannot be undone."
                : $"Discard unstaged changes to '{relativePath}'? Staged changes will be kept. This cannot be undone.";
            if (await _windowService.ShowYesNoAsync("Discard Changes", message, MessageBoxIcon.Warning,
                    _mainDockService.GetWindowOwner(this)) != MessageBoxStatus.Yes) return;
            await Task.Run(() =>
            {
                if (untracked) File.Delete(Path.Combine(repository.Info.WorkingDirectory, relativePath));
                else GitOperations.DiscardWorkingTreeFile(repository, relativePath);
            });
        });
    }

    private Task DiscardAllAsync(ResetMode mode)
    {
        return RunRepositoryOperationAsync(async repository =>
        {
            if (mode == ResetMode.Hard)
            {
                if (repository.Head.Tip == null)
                {
                    _windowService.ShowNotification("Git Info", "There is no commit to reset to. Unstage or discard individual files instead.");
                    return;
                }
                if (await _windowService.ShowYesNoAsync("Discard All Changes",
                        "Discard ALL staged and unstaged changes to tracked files? This cannot be undone. Untracked files will be kept.",
                        MessageBoxIcon.Warning, _mainDockService.GetWindowOwner(this)) != MessageBoxStatus.Yes) return;
            }
            await Task.Run(() => GitOperations.ResetTrackedChanges(repository, mode));
        });
    }

    #endregion

    #region Open & Compare

    public async Task<string?> OpenFileAsync(string path)
    {
        if (ActiveRepository?.Repository is not { } repository) return null;

        if (!Path.IsPathRooted(path)) path = Path.Combine(repository.Info.WorkingDirectory, path);
        await _mainDockService.OpenFileAsync(path);
        return path;
    }

    public Task OpenHeadFileAsync(string path)
    {
        return RunRepositoryOperationAsync(async repository =>
        {
            var relativePath = GitOperations.GetRelativePath(repository, path);
            if (repository.Head.Tip?[relativePath]?.Target is not Blob blob)
            {
                _windowService.ShowNotification("Git Info", "This file does not exist in HEAD.");
                return;
            }
            // A snapshot must never reuse (and overwrite) an open working-tree editor.
            var folder = Path.Combine(_paths.TempDirectory, "Git", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var snapshotPath = Path.Combine(folder, Path.GetFileName(relativePath));
            using (var content = blob.GetContentStream())
            await using (var file = File.Create(snapshotPath))
                await content.CopyToAsync(file);
            if (await _mainDockService.OpenFileAsync(snapshotPath) is IEditor editor)
            {
                editor.Title = Path.GetFileName(relativePath) + " (HEAD)";
                editor.IsReadOnly = true;
            }
        });
    }

    public void CompareAndSwitch(string path)
    {
        if (ActiveRepository?.Repository is not { } repository) return;
        Compare(path, true);
    }

    public void CompareStagedAndSwitch(string path)
    {
        if (ActiveRepository?.Repository is not { } repository) return;
        _ = CompareChangesAsync(repository, path, "Staged: ", 10000, staged: true);
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

    public Patch? GetPatch(string path, int contextLines, bool staged = false, string? repositoryPath = null)
    {
        repositoryPath ??= Repository.Discover(Path.GetDirectoryName(path));
        if (repositoryPath == null) return null;
        using var repository = new Repository(repositoryPath);
        return GitOperations.GetPatch(repository, path, contextLines, staged);
    }

    private async Task CompareChangesAsync(Repository repository, string path, string titlePrefix, int contextLines = 3,
        bool switchTab = true, bool staged = false)
    {
        try
        {
            var repositoryPath = repository.Info.Path;
            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(repository.Info.WorkingDirectory, path.Replace('/', Path.DirectorySeparatorChar));

            var openTab = _mainDockService.SearchView<CompareGitViewModel>()
                .FirstOrDefault(x => x.FullPath == fullPath && x.IsStaged == staged);
            openTab ??= ContainerLocator.Container.Resolve<CompareGitViewModel>((typeof(string), fullPath));

            openTab.RepositoryPath = repositoryPath;
            openTab.IsStaged = staged;
            openTab.ContextLines = contextLines;
            openTab.Title = titlePrefix + Path.GetFileName(path);
            openTab.Id = titlePrefix + fullPath;

            _mainDockService.Show(openTab, DockShowLocation.Document);
            openTab.InitializeContent();
            await Task.CompletedTask;
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
        var fullPath = await OpenFileAsync(path);
        if (fullPath == null) return;

        var evm = await _mainDockService.OpenFileAsync(fullPath);
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
        SupportedCredentialTypes types, CancellationToken cancellationToken = default, bool interactive = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (types.HasFlag(SupportedCredentialTypes.UsernamePassword) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var store = CredentialManager.Create("oneware");
            // One login attempt only; successful authentication may still fail to save credentials.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var username = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                    ? _settingsService.GetSettingValue<string>(SourceControlModule.GitHubAccountNameKey)
                    : usernameFromUrl;
                if (!string.IsNullOrWhiteSpace(username))
                {
                    var cred = store.Get($"{uri.Scheme}://{uri.Host}", username);
                    if (cred != null)
                        return new UsernamePasswordCredentials { Username = cred.Account, Password = cred.Password };
                }
                if (!interactive || attempt != 0 || !_loginProviders.TryGetValue(uri.Host, out var loginProvider) ||
                    !await LoginDialogAsync(loginProvider)) break;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new DefaultCredentials();
    }

    #endregion

    #region Identity

    private async Task<Signature?> GetSignatureAsync(Repository repository)
    {
        var author = repository.Config.BuildSignature(DateTimeOffset.Now);

        if (author == null)
        {
            var identity = await SetUserIdentityCoreAsync(repository, true);
            if (identity == null) return null;
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
            "Please enter a valid email address to sign your changes", MessageBoxIcon.Info, author?.Email);
        if (email == null) return null;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            _logger.Error("Username and/or email can't be empty", null, false, true);
            return null;
        }

        return new Identity(name.Trim(), email.Trim());
    }

    private Task SetUserIdentityAsync(bool dialog) => RunRepositoryOperationAsync(async repository =>
    {
        await SetUserIdentityCoreAsync(repository, dialog);
    });

    private async Task<Identity?> SetUserIdentityCoreAsync(Repository repository, bool dialog)
    {
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
                    // Never overwrite existing configuration; let libgit2 escape identity values.
                    using (File.Open(globalConfig, FileMode.OpenOrCreate, FileAccess.Write)) { }
                    using var config = Configuration.BuildFrom(repository.Info.Path, globalConfig);
                    config.Set("user.name", identity.Name, ConfigurationLevel.Global);
                    config.Set("user.email", identity.Email, ConfigurationLevel.Global);
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
