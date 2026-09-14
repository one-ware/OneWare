using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Installers;

namespace OneWare.PackageManager.Services;

public partial class PackageService
{
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly Dictionary<string, InstalledPackage> _installedRecords = new(StringComparer.Ordinal);
    private readonly PackageDependencyResolver _resolver = new();
    private readonly HashSet<string> _pluginsPendingRestart = new(StringComparer.Ordinal);

    private Dictionary<string, InstalledPackage> InstalledRecords() => _packages.Values
        .Where(x => x.InstalledVersion != null).ToDictionary(x => x.Package.Id!, x =>
        {
            var old = _installedRecords.GetValueOrDefault(x.Package.Id!);
            return new InstalledPackage(x.Package.Id!, x.Package.Type!, x.Package.Name ?? x.Package.Id!,
                x.Package.Category, x.Package.Description, x.Package.License, x.InstalledVersion!.Version!)
            {
                Dependencies = old != null && old.InstalledVersion == x.InstalledVersion.Version ? old.Dependencies ?? x.InstalledVersion.Dependencies : x.InstalledVersion.Dependencies,
                ResolvedDependencies = old?.ResolvedDependencies,
                Source = old?.Source ?? x.Package.SourceUrl
            };
        }, StringComparer.Ordinal);

    private PackageOperationPlan ResolvePlan(IReadOnlyList<PackageRequest> roots, CancellationToken token = default)
    {
        var plan = _resolver.Resolve(
            _packages.ToDictionary(x => x.Key, x => x.Value.Package), InstalledRecords(), roots,
            (package, version) => ResolveInstaller(package).SelectTarget(package, version) != null, token);
        if (!plan.IsValid) return plan;
        var targets = plan.Items.Where(x => x.Action != PackagePlanAction.Reuse).Select(item =>
        {
            var package = _packages[item.Id].Package;
            var version = package.Versions!.Single(x => x.Version == item.Version);
            return new { item.Id, Target = ResolveInstaller(package).SelectTarget(package, version) };
        }).ToArray();
        return plan with { Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { plan.Fingerprint, Targets = targets })))) };
    }

    public async Task<PackageOperationPlan> PlanAsync(IReadOnlyList<PackageRequest> roots,
        CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try { return ResolvePlan(roots, cancellationToken); }
        finally { _operationLock.Release(); }
    }

    private async Task<PackageInstallResult> ExecuteLegacyAsync(string id, PackageVersion? version,
        bool includePrerelease, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tracked = _installCancellation.TryAdd(id, cts);
        try
        {
            var plan = await PlanAsync([new(id, version?.Version, includePrerelease)], cts.Token);
            return await ExecuteAsync(plan, [], cts.Token);
        }
        catch (OperationCanceledException) { return new() { Status = PackageInstallResultReason.Cancelled }; }
        finally { if (tracked) _installCancellation.TryRemove(id, out _); }
    }

    public async Task<PackageInstallResult> ExecuteAsync(PackageOperationPlan plan,
        IReadOnlyCollection<string> acceptedLicenses, CancellationToken cancellationToken = default)
    {
        var lockTaken = false;
        var staged = new Dictionary<string, string>(StringComparer.Ordinal);
        var originalStatuses = new Dictionary<string, PackageStatus>(StringComparer.Ordinal);
        var completed = new List<string>();
        var restart = false;
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = operationCts.Token;
        var trackedRoots = new List<string>();
        PackageInstallResult Result(PackageInstallResultReason status, string? message = null) => new()
        { Status = status, Message = message, CompletedPackages = completed.ToArray(), RestartRequired = restart };
        try
        {
            // Register roots before waiting, so CancelInstall also cancels queued operations.
            foreach (var root in plan.Roots)
                if (_installCancellation.TryAdd(root.Id, operationCts)) trackedRoots.Add(root.Id);
            await _operationLock.WaitAsync(token);
            lockTaken = true;
            // A queued request for the same root must take ownership after its predecessor exits.
            foreach (var root in plan.Roots)
                if (!trackedRoots.Contains(root.Id) && _installCancellation.TryAdd(root.Id, operationCts))
                    trackedRoots.Add(root.Id);
            // Never trust caller-supplied items or consent after a catalog/installed-state change.
            var current = ResolvePlan(plan.Roots, token);
            if (!current.IsValid) return Result(PackageInstallResultReason.InvalidPlan, string.Join("\n", current.Errors));
            if (current.Fingerprint != plan.Fingerprint) return Result(PackageInstallResultReason.PlanChanged, "Packages changed. Review the installation again.");
            plan = current;
            var licenses = plan.Items.Where(x => x.RequiresLicense).ToArray();
            if (licenses.Any(x => !acceptedLicenses.Contains(x.Id)))
                return Result(PackageInstallResultReason.ConsentRequired, "Review and accept the licenses for: " + string.Join(", ", licenses.Select(x => x.Name)));
            var changes = plan.Items.Where(x => x.Action != PackagePlanAction.Reuse).ToArray();
            if (changes.Length == 0) return Result(PackageInstallResultReason.AlreadyInstalled);
            // Download every archive before touching any existing installation.
            foreach (var item in changes)
            {
                token.ThrowIfCancellationRequested();
                var state = _packages[item.Id];
                var package = state.Package;
                var version = package.Versions!.Single(x => x.Version == item.Version);
                var installer = ResolveInstaller(package);
                var target = installer.SelectTarget(package, version)!;
                var path = installer.GetExtractionPath(package, _paths) + ".stage-" + Guid.NewGuid().ToString("N");
                staged[item.Id] = path;
                originalStatuses[item.Id] = state.Status;
                state.Status = PackageStatus.Installing;
                var progress = new Progress<float>(value =>
                {
                    state.Progress = value;
                    state.IsIndeterminate = value >= 1;
                    PackageProgress?.Invoke(this, new(item.Id, state.Status, value, state.IsIndeterminate));
                });
                var url = target.Url ?? $"{package.SourceUrl}/{version.Version}/{package.Id}_{version.Version}_{target.Target}.zip";
                var downloaded = await _downloader.DownloadAndExtractAsync(url, path, target.IsArchive, progress, token);
                token.ThrowIfCancellationRequested();
                if (!downloaded)
                    return Result(PackageInstallResultReason.ErrorDownloading, $"Downloading {item.Name} failed. Existing installations were not changed.");
                if (!Directory.Exists(path)) throw new IOException($"No extracted files for {item.Name}.");
                PlatformHelper.ChmodFolder(path);
                var compatibility = installer is PluginPackageInstaller
                    ? PluginCompatibilityChecker.CheckCompatibilityPath(path,
                        PluginCompatibilityChecker.ReadProvidedAssemblies(DependencyPaths(item, plan, staged).Values))
                    : await installer.CheckCompatibilityAsync(package, version, token);
                if (!compatibility.IsCompatible)
                    return new PackageInstallResult { Status = PackageInstallResultReason.Incompatible,
                        Message = $"{item.Name} is incompatible.", CompatibilityRecord = compatibility };
            }

            foreach (var item in changes)
            {
                token.ThrowIfCancellationRequested();
                var state = _packages[item.Id];
                var package = state.Package;
                var installer = ResolveInstaller(package);
                var version = package.Versions!.Single(x => x.Version == item.Version);
                var target = installer.SelectTarget(package, version)!;
                var final = installer.GetExtractionPath(package, _paths);
                var backup = final + ".backup-" + Guid.NewGuid().ToString("N");
                var previous = state.InstalledVersion;
                var previousStatus = originalStatuses[item.Id];
                var previousWarning = state.InstalledVersionWarningText;
                var previousRecord = _installedRecords.GetValueOrDefault(item.Id);
                var moved = false;
                var activated = false;
                var activationAttempted = false;
                try
                {
                    if (previous != null)
                    {
                        var previousTarget = installer.SelectTarget(package, previous) ?? new PackageTarget { Target = "all" };
                        var removal = new PackageInstallContext(package, previous, previousTarget, final, new Progress<float>());
                        await installer.PrepareRemoveAsync(removal, token);
                    }
                    token.ThrowIfCancellationRequested();
                    if (Directory.Exists(final)) { Directory.Move(final, backup); moved = true; }
                    Directory.Move(staged[item.Id], final);
                    activated = true;
                    var context = new PackageInstallContext(package, version, target, final, new Progress<float>());
                    var needsRestart = package.Type == "Plugin" && (previous != null || _pluginsPendingRestart.Contains(item.Id) ||
                        DependencyPaths(item, plan, new Dictionary<string, string>()).Keys.Any(id => _packages[id].Status == PackageStatus.NeedRestart));
                    // Persist before hot loading: a write failure must not load an unrecorded assembly.
                    state.InstalledVersion = version;
                    _installedRecords[item.Id] = new InstalledPackage(item.Id, package.Type!, item.Name, package.Category,
                        package.Description, package.License, item.Version)
                    {
                        Dependencies = item.Dependencies.ToArray(),
                        ResolvedDependencies = item.Dependencies.ToDictionary(x => x.Id,
                            x => plan.Items.Single(p => p.Id == x.Id).Version),
                        Source = package.SourceUrl
                    };
                    await SaveInstalledPackagesAsync();
                    token.ThrowIfCancellationRequested();
                    activationAttempted = !needsRestart;
                    var result = needsRestart ? new PackageInstallerResult(PackageStatus.NeedRestart) :
                        installer is PluginPackageInstaller pluginInstaller
                            ? await pluginInstaller.InstallWithDependenciesAsync(context,
                                DependencyPaths(item, plan, new Dictionary<string, string>()), token)
                            : await installer.InstallAsync(context, token);
                    if (result.Status is not (PackageStatus.Installed or PackageStatus.NeedRestart))
                        throw new InvalidOperationException($"Activation failed for {item.Name}.");
                    state.Status = result.Status;
                    state.InstalledVersionWarningText = result.InstalledVersionWarningText;
                    UpdateStatus(state);
                    restart |= result.Status == PackageStatus.NeedRestart;
                    completed.Add(item.Id);
                }
                catch (Exception failure)
                {
                    state.InstalledVersion = previous;
                    state.Status = previousStatus;
                    state.InstalledVersionWarningText = previousWarning;
                    if (activationAttempted && package.Type == "Plugin")
                    {
                        _pluginsPendingRestart.Add(item.Id);
                        restart = true;
                        state.Status = PackageStatus.NeedRestart;
                    }
                    if (previousRecord == null) _installedRecords.Remove(item.Id);
                    else _installedRecords[item.Id] = previousRecord;
                    UpdateStatus(state);
                    // Restore memory even if filesystem rollback fails; never silently discard the backup.
                    var rollbackErrors = new List<Exception>();
                    try
                    {
                        if (activated && Directory.Exists(final)) Directory.Delete(final, true);
                        if (moved) Directory.Move(backup, final);
                    }
                    catch (Exception ex) { rollbackErrors.Add(ex); }
                    try { await SaveInstalledPackagesAsync(); }
                    catch (Exception ex) { rollbackErrors.Add(ex); }
                    if (rollbackErrors.Count > 0)
                        throw new AggregateException($"Activation and rollback failed for {item.Name}. Repair the installation; backup: {backup}",
                            new[] { failure }.Concat(rollbackErrors));
                    throw;
                }
                // Cleanup failure must not roll back a successfully activated package.
                try { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { _logger.Warning($"Backup retained: {backup}: {ex.Message}"); }
            }
            return Result(PackageInstallResultReason.Installed);
        }
        catch (OperationCanceledException) { return Result(PackageInstallResultReason.Cancelled, "Installation cancelled. Completed dependencies were retained."); }
        catch (Exception ex)
        {
            _logger.Error(ex.Message, ex);
            return Result(PackageInstallResultReason.ErrorDownloading, ex.Message);
        }
        finally
        {
            try
            {
                foreach (var id in trackedRoots) _installCancellation.TryRemove(id, out _);
                foreach (var (id, path) in staged)
                {
                    try { if (Directory.Exists(path)) Directory.Delete(path, true); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    { _logger.Warning($"Staging cleanup failed: {ex.Message}"); }
                    if (_packages.TryGetValue(id, out var state))
                    {
                        state.Progress = 0;
                        state.IsIndeterminate = false;
                        if (state.Status == PackageStatus.Installing) state.Status = originalStatuses[id];
                        UpdateStatus(state);
                    }
                }
            }
            finally { if (lockTaken) _operationLock.Release(); }
        }
    }

    private Dictionary<string, string> DependencyPaths(PackagePlanItem item, PackageOperationPlan plan,
        IReadOnlyDictionary<string, string> staged)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(PackagePlanItem parent)
        {
            foreach (var dep in parent.Dependencies)
                if (!paths.ContainsKey(dep.Id))
                {
                    var package = _packages[dep.Id].Package;
                    paths[dep.Id] = staged.GetValueOrDefault(dep.Id) ?? ResolveInstaller(package).GetExtractionPath(package, _paths);
                    Add(plan.Items.Single(x => x.Id == dep.Id));
                }
        }
        Add(item);
        return paths;
    }
}