using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Services;

public class OnnxRuntimePackageMigration(
    IPackageStateStore stateStore,
    IPaths paths,
    ISettingsService settingsService,
    ILogger logger)
{
    private readonly object _migrationLock = new();
    private Task? _migrationTask;

    private const string SelectedRuntimeSettingKey = "OnnxRuntime_SelectedRuntime";
    private const string SelectedExecutionProviderSettingKey = "OnnxRuntime_SelectedExecutionProvider";

    private const string CurrentPackageId = "onnxruntime-nvidia";
    private const string CurrentVersion = "1.28.0";

    private static readonly string[] RetiredPackageIds =
    [
        "onnxruntime-directml",
        "onnxruntime-openvino",
        "onnxruntime-qnn"
    ];

    public void Start()
    {
        _ = ObserveMigrationAsync();
    }

    public Task MigrateAsync()
    {
        lock (_migrationLock)
            return _migrationTask ??= MigrateInternalAsync();
    }

    private async Task MigrateInternalAsync()
    {
        var installed = await stateStore.LoadAsync();
        var retained = new Dictionary<string, InstalledPackage>(installed);
        var removedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var package in installed.Values.Where(IsOutdated))
        {
            if (!TryDeletePackageDirectory(package.Id)) continue;

            retained.Remove(package.Id);
            removedPackageIds.Add(package.Id);
        }

        foreach (var packageId in RetiredPackageIds.Where(id => !installed.ContainsKey(id)))
        {
            if (TryDeletePackageDirectory(packageId))
                removedPackageIds.Add(packageId);
        }

        if (retained.Count != installed.Count)
            await stateStore.SaveAsync(retained.Values);

        var selectedRuntime = settingsService.GetSettingValue<string>(SelectedRuntimeSettingKey);
        if (selectedRuntime == null || !removedPackageIds.Contains(selectedRuntime)) return;

        settingsService.SetSettingValue(SelectedRuntimeSettingKey, "onnxruntime-builtin");
        settingsService.SetSettingValue(SelectedExecutionProviderSettingKey, OnnxExecutionProvider.Cpu);
        settingsService.Save(paths.SettingsPath);
    }

    private async Task ObserveMigrationAsync()
    {
        try
        {
            await MigrateAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to migrate outdated ONNX Runtime packages.");
        }
    }

    private static bool IsOutdated(InstalledPackage package)
    {
        if (!string.Equals(package.Type, "OnnxRuntime", StringComparison.OrdinalIgnoreCase)) return false;

        return !string.Equals(package.Id, CurrentPackageId, StringComparison.OrdinalIgnoreCase)
               || !string.Equals(package.InstalledVersion, CurrentVersion, StringComparison.Ordinal);
    }

    private bool TryDeletePackageDirectory(string packageId)
    {
        var packageDirectory = Path.Combine(paths.OnnxRuntimesDirectory, packageId);
        if (!Directory.Exists(packageDirectory)) return true;

        try
        {
            Directory.Delete(packageDirectory, true);
            logger.LogInformation("Removed outdated ONNX Runtime package '{PackageId}'.", packageId);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Failed to remove outdated ONNX Runtime package '{PackageId}'.", packageId);
            return false;
        }
    }
}
