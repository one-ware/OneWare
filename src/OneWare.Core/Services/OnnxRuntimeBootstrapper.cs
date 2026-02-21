using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class OnnxRuntimeBootstrapper
{
    public const string SettingSelectedRuntimeKey = "OnnxRuntime_SelectedRuntime";
    public const string RuntimeProviderEnvironmentKey = "ONEWARE_ONNXRUNTIME_PROVIDER";

    private readonly ILogger _logger;
    private readonly IPaths _paths;
    private static readonly object ResolverSync = new();
    private static string? _resolverNativeDirectory;
    private static IntPtr _resolverOnnxRuntimeHandle;
    private static bool _onnxResolverRegistered;

    public string SelectedRuntime { get; private set; } = "onnxruntime-cpu";

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
            //EnsureBundledCpuRuntimeAvailable();

            var selectedRuntime = ResolveConfiguredValue(SettingSelectedRuntimeKey, RuntimeProviderEnvironmentKey)
                                  ?.Trim();
            if (string.IsNullOrWhiteSpace(selectedRuntime)) selectedRuntime = "onnxruntime-cpu";
            SelectedRuntime = selectedRuntime;
            
            var selectedRuntimeRoot = Path.Combine(_paths.OnnxRuntimesDirectory, selectedRuntime);
            if (TryLoadFromRoot(selectedRuntimeRoot, $"runtime '{selectedRuntime}'"))
                return;

            if (NativeLibrary.TryLoad("onnxruntime", out var existingHandle))
            {
                ConfigureOnnxRuntimeDllImportResolver(null, existingHandle);
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

    private void EnsureBundledCpuRuntimeAvailable()
    {
        try
        {
            var targetPath = Path.Combine(_paths.OnnxRuntimesDirectory, "onnxruntime-cpu");
            if (Directory.Exists(targetPath)) return;

            var sourcePathCandidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BundledOnnxRuntimes", "onnxruntime-cpu"),
                Path.Combine(Path.GetDirectoryName(typeof(OnnxRuntimeBootstrapper).Assembly.Location) ??
                             AppDomain.CurrentDomain.BaseDirectory, "BundledOnnxRuntimes", "onnxruntime-cpu")
            };

            var sourcePath = sourcePathCandidates.FirstOrDefault(Directory.Exists);
            if (sourcePath == null) return;

            PlatformHelper.CopyDirectory(sourcePath, targetPath);
            _logger.LogInformation("Seeded bundled ONNX Runtime CPU package to {Path}", targetPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to seed bundled ONNX Runtime CPU package.");
        }
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

        var fileNames = GetOnnxRuntimeFileNameCandidates();
        var providersShared = PlatformHelper.GetLibraryFileName("onnxruntime_providers_shared");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            TrySetDllDirectory(nativeDirectory);

        // Ensure provider shared library can be resolved before loading onnxruntime itself.
        var providerSharedPath = Path.Combine(nativeDirectory, providersShared);
        if (File.Exists(providerSharedPath))
            _ = NativeLibrary.TryLoad(providerSharedPath, out _);

        foreach (var fileName in fileNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.Combine(nativeDirectory, fileName);
            if (!File.Exists(fullPath)) continue;

            if (!NativeLibrary.TryLoad(fullPath, out var handle)) continue;

            ConfigureOnnxRuntimeDllImportResolver(nativeDirectory, handle);
            _logger.LogInformation("Loaded ONNX Runtime from {Path} ({Source})", fullPath, source);
            return true;
        }

        return false;
    }

    private IEnumerable<string> EnumerateNativeSearchDirectories(string rootPath)
    {
        var directories = new List<string>();
        var runtimesRoot = Path.Combine(rootPath, "runtimes");

        directories.Add(rootPath);
        directories.AddRange(GetRidCandidates()
            .Select(rid => Path.Combine(runtimesRoot, rid, "native")));

        try
        {
            if (Directory.Exists(runtimesRoot))
            {
                var familyPrefix = GetRidFamilyPrefix();
                var architectureSuffix = GetRidArchitectureSuffix();
                directories.AddRange(
                    Directory.EnumerateDirectories(runtimesRoot)
                        .Select(path => new
                        {
                            Name = Path.GetFileName(path),
                            NativePath = Path.Combine(path, "native")
                        })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                        .Where(x =>
                            x.Name.StartsWith(familyPrefix, StringComparison.OrdinalIgnoreCase) &&
                            x.Name.EndsWith(architectureSuffix, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.NativePath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to search native subdirectories under '{RootPath}'.", rootPath);
        }

        return directories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetOnnxRuntimeFileNameCandidates()
    {
        var baseName = PlatformHelper.GetLibraryFileName("onnxruntime");
        var candidates = new List<string> { baseName };

        if (!baseName.StartsWith("lib", StringComparison.OrdinalIgnoreCase))
            candidates.Add($"lib{baseName}");

        return candidates;
    }

    private void ConfigureOnnxRuntimeDllImportResolver(string? nativeDirectory, IntPtr onnxRuntimeHandle)
    {
        lock (ResolverSync)
        {
            if (!string.IsNullOrWhiteSpace(nativeDirectory))
                _resolverNativeDirectory = nativeDirectory;

            if (onnxRuntimeHandle != IntPtr.Zero)
                _resolverOnnxRuntimeHandle = onnxRuntimeHandle;

            if (_onnxResolverRegistered) return;

            try
            {
                var onnxAssembly = typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly;
                NativeLibrary.SetDllImportResolver(onnxAssembly, ResolveOnnxRuntimeNativeLibrary);
                _onnxResolverRegistered = true;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogDebug(ex, "ONNX Runtime DllImportResolver already set by another component.");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to configure ONNX Runtime DllImportResolver.");
            }
        }
    }

    private static IntPtr ResolveOnnxRuntimeNativeLibrary(string libraryName, Assembly _, DllImportSearchPath? __)
    {
        if (!libraryName.Contains("onnxruntime", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        lock (ResolverSync)
        {
            if (libraryName.Equals("onnxruntime", StringComparison.OrdinalIgnoreCase) &&
                _resolverOnnxRuntimeHandle != IntPtr.Zero)
                return _resolverOnnxRuntimeHandle;

            if (string.IsNullOrWhiteSpace(_resolverNativeDirectory) || !Directory.Exists(_resolverNativeDirectory))
                return IntPtr.Zero;

            foreach (var candidate in BuildLibraryFileCandidates(libraryName))
            {
                var fullPath = Path.Combine(_resolverNativeDirectory, candidate);
                if (!File.Exists(fullPath)) continue;
                if (NativeLibrary.TryLoad(fullPath, out var handle))
                    return handle;
            }

            return IntPtr.Zero;
        }
    }

    private static IEnumerable<string> BuildLibraryFileCandidates(string libraryName)
    {
        var candidates = new List<string>();
        var fileName = Path.GetFileName(libraryName);

        if (!string.IsNullOrWhiteSpace(fileName))
            candidates.Add(fileName);

        if (!Path.HasExtension(fileName))
            candidates.Add(PlatformHelper.GetLibraryFileName(fileName));

        if (!fileName.StartsWith("lib", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add($"lib{fileName}");
            if (!Path.HasExtension(fileName))
                candidates.Add($"lib{PlatformHelper.GetLibraryFileName(fileName)}");
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetRidCandidates()
    {
        var candidates = new[]
        {
            RuntimeInformation.RuntimeIdentifier,
            PlatformHelper.PlatformIdentifier
        };

        return candidates
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetRidFamilyPrefix()
    {
        return PlatformHelper.Platform switch
        {
            PlatformId.WinX64 => "win-",
            PlatformId.WinArm64 => "win-",
            PlatformId.LinuxX64 => "linux-",
            PlatformId.LinuxArm64 => "linux-",
            PlatformId.OsxX64 => "osx-",
            PlatformId.OsxArm64 => "osx-",
            _ => string.Empty
        };
    }

    private static string GetRidArchitectureSuffix()
    {
        return PlatformHelper.Platform switch
        {
            PlatformId.WinX64 => "-x64",
            PlatformId.LinuxX64 => "-x64",
            PlatformId.OsxX64 => "-x64",
            PlatformId.WinArm64 => "-arm64",
            PlatformId.LinuxArm64 => "-arm64",
            PlatformId.OsxArm64 => "-arm64",
            _ => string.Empty
        };
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
