using Avalonia.Media;
using Avalonia.Threading;
using GitCredentialManager;
using LibGit2Sharp;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.SourceControl.LoginProviders;
using OneWare.SourceControl.ViewModels;
using OneWare.SourceControl.Views;
using Prism.Ioc;

namespace OneWare.SourceControl.Services;

public class GitService
{
    private readonly IApplicationStateService _applicationStateService;
    private readonly IWindowService _windowService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    
    public GitService(IApplicationStateService applicationStateService, IWindowService windowService, ISettingsService settingsService, ILogger logger)
    {
        _applicationStateService = applicationStateService;
        _windowService = windowService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<bool> CloneRepositoryAsync(string url, string destination)
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

    #region Branches

    public void ChangeBranch(Repository repository, Branch branch)
    {
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
            
            _logger.Log($"Switched to branch '{branch.FriendlyName}'", ConsoleColor.Green, true, Brushes.Green);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
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
        var vm = new AuthenticateViewModel(loginProvider);
        await Dispatcher.UIThread.InvokeAsync(() => _windowService.ShowDialogAsync(new AuthenticateGitView()
        {
            DataContext = vm
        }));
        return vm.Success;
    }

    public async Task<Credentials> GetCredentialsAsync(string url, string usernameFromUrl,
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
                    return new UsernamePasswordCredentials()
                    {
                        Username = cred.Account,
                        Password = cred.Password
                    };
            }

            var loginTask = ub.Host switch
            {
                "github.com" => LoginGithubAsync(),
                _ => Task.FromResult(false)
            };
            
            var result = await loginTask;

            if(cancellationToken.IsCancellationRequested) return new DefaultCredentials();
            
            if (result)
            {
                return await GetCredentialsAsync(url, usernameFromUrl, types, cancellationToken);
            }
        }
        return new DefaultCredentials();
    }
    
    #endregion
}