using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class OnnxRuntimeBootstrapper
{
    public const string SettingSelectedRuntimeKey = "OnnxRuntime_SelectedRuntime";
    public const string SettingRuntimePathKey = "OnnxRuntime_RuntimePath";
    public const string RuntimeProviderEnvironmentKey = "ONEWARE_ONNXRUNTIME_PROVIDER";

    private readonly ILogger _logger;
    private readonly IPaths _paths;
    private string? _loadedRuntimeLibraryPath;
    private bool _resolverInstalled;

    public string SelectedRuntime { get; private set; } = "cpu";

    public OnnxRuntimeBootstrapper(IPaths paths, ILogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public void Initialize()
    {
        if (PlatformHelper.Platform is PlatformId.Wasm) return;

        try
        {
            var selectedRuntime = ResolveConfiguredValue(SettingSelectedRuntimeKey, RuntimeProviderEnvironmentKey)
                                  ?.Trim();
            if (string.IsNullOrWhiteSpace(selectedRuntime)) selectedRuntime = "cpu";
            SelectedRuntime = selectedRuntime;

            if (selectedRuntime.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                selectedRuntime.Equals("disabled", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("ONNX Runtime preload disabled by configuration.");
                return;
            }
            
            foreach (var runtimeName in ResolveRuntimeCandidates(selectedRuntime))
            {
                var runtimeRoot = Path.Combine(_paths.OnnxRuntimesDirectory, runtimeName);
                if (TryLoadFromRoot(runtimeRoot, $"runtime '{runtimeName}'"))
                {
                    InstallOnnxRuntimeResolver();
                    return;
                }
            }

            if (NativeLibrary.TryLoad("onnxruntime", out var existingHandle))
            {
                _logger.LogInformation("Loaded default ONNX Runtime from bundled/probing paths: {Handle}",
                    existingHandle);
                return;
            }

            _logger.LogInformation(
                "ONNX Runtime preload skipped. No matching runtime found in '{Path}'.", _paths.OnnxRuntimesDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize ONNX Runtime bootstrapper.");
        }
    }

    private IEnumerable<string> ResolveRuntimeCandidates(string selectedRuntime)
    {
        var candidates = new List<string>();
        if (!selectedRuntime.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(selectedRuntime);
            return candidates;
        }

        switch (PlatformHelper.Platform)
        {
            case PlatformId.LinuxX64:
            case PlatformId.LinuxArm64:
                candidates.Add("gpu-linux");
                candidates.Add("cuda");
                candidates.Add("cpu");
                break;
            case PlatformId.WinX64:
            case PlatformId.WinArm64:
                candidates.Add("directml");
                candidates.Add("cuda");
                candidates.Add("cpu");
                break;
            default:
                candidates.Add("cpu");
                break;
        }

        try
        {
            if (Directory.Exists(_paths.OnnxRuntimesDirectory))
                candidates.AddRange(Directory.GetDirectories(_paths.OnnxRuntimesDirectory)
                    .Select(Path.GetFileName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))!
                    .Cast<string>());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enumerate ONNX runtime folders.");
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool TryLoadFromRoot(string rootPath, string source)
    {
        if (!Directory.Exists(rootPath)) return false;

        foreach (var nativeDirectory in EnumerateNativeSearchDirectories(rootPath))
            if (TryLoadFromNativeDirectory(nativeDirectory, source))
                return true;

        return false;
    }

    private bool TryLoadFromNativeDirectory(string nativeDirectory, string source)
    {
        if (!Directory.Exists(nativeDirectory)) return false;

        var fileNames = new[]
        {
            PlatformHelper.GetLibraryFileName("onnxruntime"),
            $"lib{PlatformHelper.GetLibraryFileName("onnxruntime")}"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            TrySetDllDirectory(nativeDirectory);

        foreach (var fileName in fileNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.Combine(nativeDirectory, fileName);
            if (!File.Exists(fullPath)) continue;

            if (!NativeLibrary.TryLoad(fullPath, out var handle)) continue;

            _loadedRuntimeLibraryPath = fullPath;
            _logger.LogInformation("Loaded ONNX Runtime from {Path} ({Source})", fullPath, source);
            return true;
        }

        return false;
    }

    private void InstallOnnxRuntimeResolver()
    {
        if (_resolverInstalled || string.IsNullOrWhiteSpace(_loadedRuntimeLibraryPath)) return;

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(SessionOptions).Assembly, (libraryName, _, _) =>
            {
                if (!libraryName.Contains("onnxruntime", StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                return NativeLibrary.TryLoad(_loadedRuntimeLibraryPath, out var handle) ? handle : IntPtr.Zero;
            });

            _resolverInstalled = true;
            _logger.LogInformation("Installed ONNX Runtime resolver for managed bindings: {Path}",
                _loadedRuntimeLibraryPath);
        }
        catch (InvalidOperationException)
        {
            _resolverInstalled = true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to install ONNX Runtime DllImportResolver.");
        }
    }

    private IEnumerable<string> EnumerateNativeSearchDirectories(string rootPath)
    {
        var directories = new List<string>();
        var ridNative = Path.Combine(rootPath, "runtimes", PlatformHelper.PlatformIdentifier, "native");

        directories.Add(rootPath);
        directories.Add(ridNative);

        try
        {
            directories.AddRange(Directory.EnumerateDirectories(rootPath, "native", SearchOption.AllDirectories));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to search native subdirectories under '{RootPath}'.", rootPath);
        }

        return directories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string? ResolveConfiguredValue(string settingKey, string environmentKey)
    {
        var envValue = Environment.GetEnvironmentVariable(environmentKey);
        if (!string.IsNullOrWhiteSpace(envValue)) return envValue;

        return ReadStringSetting(settingKey);
    }

    private string? ReadStringSetting(string key)
    {
        if (!File.Exists(_paths.SettingsPath)) return null;

        try
        {
            using var stream = File.OpenRead(_paths.SettingsPath);
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty(key, out var element)) return null;
            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read setting '{SettingKey}' from settings file.", key);
            return null;
        }
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string? lpPathName);

    private void TrySetDllDirectory(string directory)
    {
        try
        {
            _ = SetDllDirectory(directory);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to set DLL directory to '{Directory}'.", directory);
        }
    }
}
