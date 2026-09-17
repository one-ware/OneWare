using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.LanguageService;

public abstract class LanguageServiceLspAutoDownload : LanguageServiceLsp
{
    private readonly Package _package;
    private readonly IPackageService _packageService;
    private readonly object _lifecycleGate = new();
    private Task? _activationTask;
    private bool _activating;
    private bool _restarting;
    private bool _pendingPathActivation;
    private bool _enableAutoDownload;
    private bool _activationRequested;
    private int _activationGeneration;
    private DateTime _lastInstallAttempt = DateTime.MinValue;

    protected LanguageServiceLspAutoDownload(IObservable<string> executablePath, Package package, string name,
        string? workspace, IPackageService packageService, IObservable<bool> enableAutoDownload,
        string? arguments = null)
        : base(name, workspace)
    {
        _package = package;
        _packageService = packageService;

        Arguments = arguments;

        enableAutoDownload.Subscribe(x => { _enableAutoDownload = x; });
        executablePath.Subscribe(x =>
        {
            lock (_lifecycleGate)
            {
                if (ExecutablePath == x) return;
                ExecutablePath = x;
                // LanguageManager activates after construction, not during initial subscription.
                if (!_activationRequested || !File.Exists(ExecutablePath)) return;
                _pendingPathActivation = true;
                if (_restarting) return;
                if (IsActivated)
                    _ = RestartAsync();
                else if (!_activating)
                    _ = ActivateAsync();
            }
        });
    }

    public override Task ActivateAsync()
    {
        lock (_lifecycleGate)
        {
            if (_restarting || _activating) return Task.CompletedTask;
            _activationRequested = true;
            _activating = true;
            return _activationTask = ActivateCoreAsync(_activationGeneration);
        }
    }

    private async Task ActivateCoreAsync(int generation)
    {
        try
        {
            if (IsActivated) return;
            if (!PlatformHelper.Exists(ExecutablePath ?? "") && _enableAutoDownload)
            {
                // Opening more documents must not repeatedly retry a failed/offline download.
                if (DateTime.UtcNow - _lastInstallAttempt < TimeSpan.FromSeconds(30)) return;
                _lastInstallAttempt = DateTime.UtcNow;
                var result = await _packageService.InstallAsync(_package);
                if (result.Status is not (PackageInstallResultReason.Installed or PackageInstallResultReason.AlreadyInstalled))
                {
                    ContainerLocator.Container?.Resolve<ILogger>()
                        .Warning($"Could not install {Name}: {result.Status}. Install it from Package Manager or set its executable path.");
                    return;
                }
            }

            Task activation;
            lock (_lifecycleGate)
            {
                if (!_activationRequested || generation != _activationGeneration) return;
                _pendingPathActivation = false;
                activation = ActivateServerAsync();
            }
            await activation;
        }
        finally
        {
            lock (_lifecycleGate)
            {
                _activating = false;
                // An installer can publish the replacement while the killed server is still cleaning up.
                if (_pendingPathActivation && _activationRequested && !_restarting &&
                    generation == _activationGeneration && File.Exists(ExecutablePath))
                {
                    _pendingPathActivation = false;
                    _ = ActivateAsync();
                }
            }
        }
    }

    protected virtual Task ActivateServerAsync() => base.ActivateAsync();

    public override Task DeactivateAsync()
    {
        lock (_lifecycleGate)
        {
            _activationRequested = false;
            _pendingPathActivation = false;
            _activationGeneration++;
            return DeactivateServerAsync();
        }
    }

    protected virtual Task DeactivateServerAsync() => base.DeactivateAsync();

    public override async Task RestartAsync()
    {
        Task? outgoing;
        Task deactivation;
        int generation;
        lock (_lifecycleGate)
        {
            if (_restarting) return;
            _restarting = true;
            outgoing = _activationTask;
            deactivation = DeactivateAsync();
            generation = _activationGeneration;
        }

        var stopped = false;
        Task activation = Task.CompletedTask;
        try
        {
            await deactivation;
            // Wait for this particular outgoing server/install, never a replacement's lifetime.
            if (outgoing != null) await outgoing;
            stopped = true;
        }
        finally
        {
            lock (_lifecycleGate)
            {
                _restarting = false;
                if (stopped && generation == _activationGeneration)
                {
                    _lastInstallAttempt = DateTime.MinValue;
                    activation = ActivateAsync();
                }
            }
        }
        await activation;
    }
}
