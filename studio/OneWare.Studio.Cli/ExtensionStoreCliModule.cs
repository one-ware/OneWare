using System.Collections.ObjectModel;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OneWare.CloudIntegration;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Installers;
using OneWare.PackageManager.Services;

public sealed class ExtensionStoreCliModule : OneWareCliModuleBase
{
    private const string PluginType = "Plugin";
    private const string PublicRepository =
        "https://raw.githubusercontent.com/one-ware/OneWare.PublicPackages/main/oneware-packages.json";

    public override string Id => "extension-store";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IApplicationStateService, CliApplicationStateService>();
        services.AddSingleton<ICompositeServiceProvider>(provider => new CliCompositeServiceProvider(provider));
        services.AddSingleton<IPackageRepositoryClient, PackageRepositoryClient>();
        services.AddSingleton<IPackageCatalog, PackageCatalog>();
        services.AddSingleton<IPackageStateStore, PackageStateStore>();
        services.AddSingleton<IPackageDownloader, PackageDownloader>();
        services.AddSingleton<GenericPackageInstaller>();
        services.AddSingleton<CliPluginPackageInstaller>();
        services.AddSingleton<IPackageService, PackageService>();
    }

    public override IReadOnlyList<Command> RegisterCommands(IServiceProvider serviceProvider)
    {
        var extensions = new Command("extensions", "Search and manage installed OneWare extensions");
        extensions.Aliases.Add("extension");

        var search = new Command("search", "Search available extensions");
        var searchQuery = new Argument<string?>("query")
        {
            Description = "Text to match in an extension ID, name, or description",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => null
        };
        search.Arguments.Add(searchQuery);
        search.SetAction((parseResult, cancellationToken) =>
            SearchAsync(serviceProvider, parseResult.GetValue(searchQuery), cancellationToken));

        var list = new Command("list", "List installed extensions");
        list.Aliases.Add("installed");
        list.SetAction((_, cancellationToken) => ListAsync(serviceProvider, cancellationToken));

        var install = new Command("install", "Install an extension");
        var installId = new Argument<string>("id") { Description = "Extension ID" };
        var installVersion = new Option<string?>("--version") { Description = "Version to install" };
        installVersion.Aliases.Add("-v");
        var installPrerelease = new Option<bool>("--prerelease")
        {
            Description = "Allow a prerelease version when no version is supplied"
        };
        var installIgnoreCompatibility = new Option<bool>("--ignore-compatibility")
        {
            Description = "Install even when the extension is incompatible with this Studio version"
        };
        install.Arguments.Add(installId);
        install.Options.Add(installVersion);
        install.Options.Add(installPrerelease);
        install.Options.Add(installIgnoreCompatibility);
        install.SetAction((parseResult, cancellationToken) => InstallAsync(
            serviceProvider,
            parseResult.GetValue(installId) ?? string.Empty,
            parseResult.GetValue(installVersion),
            parseResult.GetValue(installPrerelease),
            parseResult.GetValue(installIgnoreCompatibility),
            cancellationToken));

        var update = new Command("update", "Update one extension, or all installed extensions when no ID is supplied");
        var updateId = new Argument<string?>("id")
        {
            Description = "Installed extension ID",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => null
        };
        var updatePrerelease = new Option<bool>("--prerelease") { Description = "Allow prerelease updates" };
        var updateIgnoreCompatibility = new Option<bool>("--ignore-compatibility")
        {
            Description = "Update even when an extension is incompatible with this Studio version"
        };
        update.Arguments.Add(updateId);
        update.Options.Add(updatePrerelease);
        update.Options.Add(updateIgnoreCompatibility);
        update.SetAction((parseResult, cancellationToken) => UpdateAsync(
            serviceProvider,
            parseResult.GetValue(updateId),
            parseResult.GetValue(updatePrerelease),
            parseResult.GetValue(updateIgnoreCompatibility),
            cancellationToken));

        var uninstall = new Command("uninstall", "Uninstall an extension");
        uninstall.Aliases.Add("remove");
        var uninstallId = new Argument<string>("id") { Description = "Installed extension ID" };
        uninstall.Arguments.Add(uninstallId);
        uninstall.SetAction((parseResult, cancellationToken) =>
            UninstallAsync(serviceProvider, parseResult.GetValue(uninstallId) ?? string.Empty, cancellationToken));

        extensions.Subcommands.Add(search);
        extensions.Subcommands.Add(list);
        extensions.Subcommands.Add(install);
        extensions.Subcommands.Add(update);
        extensions.Subcommands.Add(uninstall);
        return [extensions];
    }

    public static bool IsExtensionStoreCommand(IEnumerable<string> args)
    {
        var arguments = args.ToArray();
        if (arguments.Any(arg => string.Equals(arg, "oneai", StringComparison.OrdinalIgnoreCase)))
            return false;

        return arguments.Any(arg => string.Equals(arg, "extensions", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(arg, "extension", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<int> SearchAsync(IServiceProvider services, string? query, CancellationToken cancellationToken)
    {
        var packages = await RefreshAsync(services, cancellationToken);
        if (packages is null)
            return 1;

        var matches = packages.Values
            .Where(IsPlugin)
            .Where(state => string.IsNullOrWhiteSpace(query) || Matches(state.Package, query))
            .OrderBy(state => state.Package.Name ?? state.Package.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            Console.WriteLine("No matching extensions found.");
            return 0;
        }

        foreach (var state in matches)
            Console.WriteLine($"{Display(state.Package.Id)}\t{Display(state.Package.Name)}\t{Display(LatestVersion(state.Package))}\t{state.Status}");

        return 0;
    }

    private static async Task<int> ListAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var packages = await RefreshAsync(services, cancellationToken);
        if (packages is null)
            return 1;

        var installed = packages.Values
            .Where(IsPlugin)
            .Where(state => state.InstalledVersion is not null)
            .OrderBy(state => state.Package.Name ?? state.Package.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (installed.Length == 0)
        {
            Console.WriteLine("No extensions are installed.");
            return 0;
        }

        foreach (var state in installed)
            Console.WriteLine($"{Display(state.Package.Id)}\t{Display(state.Package.Name)}\t{Display(state.InstalledVersion?.Version)}\t{state.Status}");

        return 0;
    }

    private static async Task<int> InstallAsync(IServiceProvider services, string id, string? version,
        bool prerelease, bool ignoreCompatibility, CancellationToken cancellationToken)
    {
        var packages = await RefreshAsync(services, cancellationToken);
        if (packages is null)
            return 1;

        if (!packages.TryGetValue(id, out var state) || !IsPlugin(state))
        {
            Console.Error.WriteLine($"Extension '{Display(id)}' was not found.");
            return 1;
        }

        var selectedVersion = FindVersion(state.Package, version);
        if (version is not null && selectedVersion is null)
        {
            Console.Error.WriteLine($"Version '{Display(version)}' is not available for extension '{Display(id)}'.");
            return 1;
        }

        var result = await services.GetRequiredService<IPackageService>()
            .InstallAsync(id, selectedVersion, prerelease, ignoreCompatibility, cancellationToken);
        return ReportOperation("install", id, result);
    }

    private static async Task<int> UpdateAsync(IServiceProvider services, string? id, bool prerelease,
        bool ignoreCompatibility, CancellationToken cancellationToken)
    {
        var packages = await RefreshAsync(services, cancellationToken);
        if (packages is null)
            return 1;

        var selected = string.IsNullOrWhiteSpace(id)
            ? packages.Values.Where(IsPlugin).Where(state => state.InstalledVersion is not null)
                .Where(state => prerelease || HasUpdate(state)).ToArray()
            : packages.TryGetValue(id, out var state) && IsPlugin(state) && state.InstalledVersion is not null
                ? [state]
                : [];

        if (selected.Length == 0)
        {
            Console.Error.WriteLine(string.IsNullOrWhiteSpace(id)
                ? "No extension updates are available."
                : $"Installed extension '{Display(id)}' was not found.");
            return string.IsNullOrWhiteSpace(id) ? 0 : 1;
        }

        if (!string.IsNullOrWhiteSpace(id) && !prerelease && !HasUpdate(selected[0]))
        {
            Console.WriteLine($"Extension '{Display(id)}' is already up to date.");
            return 0;
        }

        var packageService = services.GetRequiredService<IPackageService>();
        var exitCode = 0;
        foreach (var extension in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await packageService.UpdateAsync(extension.Package.Id!, null, prerelease,
                ignoreCompatibility, cancellationToken);
            exitCode = Math.Max(exitCode, ReportOperation("update", extension.Package.Id!, result));
        }

        return exitCode;
    }

    private static async Task<int> UninstallAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var packages = await RefreshAsync(services, cancellationToken);
        if (packages is null)
            return 1;

        if (!packages.TryGetValue(id, out var state) || !IsPlugin(state) || state.InstalledVersion is null)
        {
            Console.Error.WriteLine($"Installed extension '{Display(id)}' was not found.");
            return 1;
        }

        var removed = await services.GetRequiredService<IPackageService>().RemoveAsync(id);
        if (!removed)
        {
            Console.Error.WriteLine($"Unable to uninstall extension '{Display(id)}'.");
            return 1;
        }

        Console.WriteLine($"Uninstalled extension '{Display(id)}'.");
        return 0;
    }

    private static async Task<IReadOnlyDictionary<string, IPackageState>?> RefreshAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            ConfigurePackageSources(services);
            var packageService = services.GetRequiredService<IPackageService>();
            packageService.RegisterInstaller<CliPluginPackageInstaller>(PluginType);
            if (!await packageService.RefreshAsync(true))
            {
                Console.Error.WriteLine("Unable to refresh the extension catalog.");
                return null;
            }

            return packageService.Packages;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Console.Error.WriteLine("Unable to initialize the extension catalog.");
            return null;
        }
    }

    private static void ConfigurePackageSources(IServiceProvider services)
    {
        var settings = services.GetRequiredService<ISettingsService>();
        var paths = services.GetRequiredService<IPaths>();

        if (!settings.HasSetting(OneWareCloudIntegrationModule.OneWareCloudHostKey))
            settings.Register(OneWareCloudIntegrationModule.OneWareCloudHostKey, OneWareCloudIntegrationModule.OfficialHost);
        if (!settings.HasSetting("PackageManager_Sources"))
            settings.Register("PackageManager_Sources", new ObservableCollection<string>());
        if (!settings.HasSetting("PackageManager_OnlyCustomSources"))
            settings.Register("PackageManager_OnlyCustomSources", false);

        settings.Load(paths.SettingsPath);

        var sources = Environment.GetEnvironmentVariable("ONEWARE_PACKAGE_REPOSITORY");
        var repositories = !string.IsNullOrWhiteSpace(sources)
            ? sources.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [
                new Uri(new Uri(settings.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareCloudHostKey)),
                    "api/studio/packages").AbsoluteUri,
                PublicRepository
            ];

        services.GetRequiredService<IPackageService>().RegisterPackageRepositoryWithFallback(repositories);
    }

    private static bool IsPlugin(IPackageState state)
    {
        return string.Equals(state.Package.Type, PluginType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(Package package, string query)
    {
        return package.Id?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
               package.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
               package.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasUpdate(IPackageState state)
    {
        return state.Status is PackageStatus.UpdateAvailable or PackageStatus.UpdateAvailablePrerelease;
    }

    private static PackageVersion? FindVersion(Package package, string? requestedVersion)
    {
        return requestedVersion is null
            ? null
            : package.Versions?.FirstOrDefault(version =>
                string.Equals(version.Version, requestedVersion, StringComparison.OrdinalIgnoreCase));
    }

    private static string? LatestVersion(Package package)
    {
        return package.Versions?.LastOrDefault()?.Version;
    }

    private static int ReportOperation(string operation, string id, PackageInstallResult result)
    {
        switch (result.Status)
        {
            case PackageInstallResultReason.Installed:
                Console.WriteLine($"{char.ToUpperInvariant(operation[0])}{operation[1..]}d extension '{Display(id)}'.");
                return 0;
            case PackageInstallResultReason.AlreadyInstalled:
                Console.WriteLine($"Extension '{Display(id)}' is already up to date.");
                return 0;
            case PackageInstallResultReason.Incompatible:
                Console.Error.WriteLine($"Extension '{Display(id)}' is incompatible with this Studio version.");
                if (!string.IsNullOrWhiteSpace(result.CompatibilityRecord?.Report))
                    Console.Error.WriteLine(Display(result.CompatibilityRecord.Report));
                return 1;
            default:
                Console.Error.WriteLine($"Unable to {operation} extension '{Display(id)}'.");
                return 1;
        }
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : new string(value.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
    }
}

internal sealed class CliPluginPackageInstaller(IHttpService httpService) : PackageInstallerBase
{
    public override string GetExtractionPath(Package package, IPaths paths)
    {
        if (string.IsNullOrWhiteSpace(package.Id))
            throw new InvalidOperationException("Package Id is required.");

        return Path.Combine(paths.PluginsDirectory, package.Id);
    }

    public override async Task<CompatibilityReport> CheckCompatibilityAsync(Package package, PackageVersion version,
        CancellationToken cancellationToken = default)
    {
        if (version.CompatibilityUrl is null && package.SourceUrl is null)
            return new CompatibilityReport(true);

        var compatibilityUrl = version.CompatibilityUrl ??
                               $"{package.SourceUrl}/{version.Version}/compatibility.txt";
        var compatibility = await httpService.DownloadTextAsync(compatibilityUrl, cancellationToken: cancellationToken);
        return compatibility is null
            ? new CompatibilityReport(true)
            : PluginCompatibilityChecker.CheckCompatibility(compatibility);
    }

    public override Task<PackageInstallerResult> InstallAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageInstallerResult(PackageStatus.Installed));
    }

    public override Task<PackageInstallerResult> RemoveAsync(PackageInstallContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PackageInstallerResult(PackageStatus.Available));
    }
}

internal sealed class CliCompositeServiceProvider(IServiceProvider provider) : ICompositeServiceProvider
{
    public object? GetService(Type serviceType) => provider.GetService(serviceType);

    public bool IsService(Type serviceType) => provider.GetService(serviceType) is not null;
}

internal sealed class CliApplicationStateService : IApplicationStateService
{
    private readonly List<Func<Task<bool>>> _shutdownTasks = [];

    public bool ShutdownComplete { get; private set; }
    public ObservableCollection<ApplicationNotification> CurrentNotifications { get; } = [];
    public ApplicationProcess ActiveProcess { get; private set; } = new() { State = AppState.Idle, StatusMessage = "Ready" };

    public ApplicationProcess AddState(string status, AppState state, Action? terminate = null)
    {
        ActiveProcess = new ApplicationProcess { StatusMessage = status, State = state, Terminate = terminate };
        return ActiveProcess;
    }

    public void RemoveState(ApplicationProcess key, string finishMessage = "Done")
    {
        key.FinishMessage = finishMessage;
        ActiveProcess = new ApplicationProcess { State = AppState.Idle, StatusMessage = finishMessage };
    }

    public Task TerminateActiveDialogAsync()
    {
        ActiveProcess.Terminate?.Invoke();
        return Task.CompletedTask;
    }

    public void RegisterAutoLaunchAction(Action<string?> action) { }
    public void RegisterPathLaunchAction(Action<string?> action) { }
    public void RegisterUrlLaunchAction(string key, Action<string?> action) { }
    public void RegisterShutdownAction(Action action) { }
    public void RegisterShutdownTask(Func<Task<bool>> task) => _shutdownTasks.Add(task);
    public void ExecuteAutoLaunchActions(string? value) { }
    public void ExecutePathLaunchActions(string? value) { }
    public void ExecuteUrlLaunchActions(Uri uri) { }

    public async Task<bool> TryShutdownAsync()
    {
        foreach (var task in _shutdownTasks)
            if (!await task())
                return false;

        ShutdownComplete = true;
        return true;
    }

    public Task<bool> TryRestartAsync() => TryShutdownAsync();
    public void AddNotification(ApplicationNotification notification) => CurrentNotifications.Add(notification);
    public void ClearNotifications() => CurrentNotifications.Clear();
}
