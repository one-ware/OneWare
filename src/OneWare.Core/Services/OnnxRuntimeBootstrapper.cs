using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class OnnxRuntimeBootstrapper
{
    public const string SettingSelectedRuntimeKey = "OnnxRuntime_SelectedRuntime";
    
    public const string SettingSelectedExecutionProviderKey = "OnnxRuntime_SelectedExecutionProvider";

    private readonly ILogger _logger;
    private readonly IPaths _paths;
    private static readonly Lock ResolverSync = new();
    private static string? _resolverNativeDirectory;
    private static IntPtr _resolverOnnxRuntimeHandle;
    private static bool _onnxResolverRegistered;

    public string SelectedRuntime { get; private set; } = "onnxruntime-builtin";

    public OnnxRuntimeBootstrapper(IPaths paths, ILogger logger)
    {
        _paths = paths;
        _logger = logger;
    }
    
    public static string[] GetOnnxRuntimeOptions(IPaths paths)
    {
        var options = new List<string> { "onnxruntime-builtin" };
        try
        {
            if (Directory.Exists(paths.OnnxRuntimesDirectory))
                options.AddRange(Directory.GetDirectories(paths.OnnxRuntimesDirectory)
                    .Select(Path.GetFileName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))!
                    .Cast<string>());
        }
        catch
        {
            // Ignore IO errors and keep default options.
        }

        return options
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    
    public OnnxExecutionProvider[] GetOnnxExecutionProviders()
    {
        var executionProviders = new List<OnnxExecutionProvider> { OnnxExecutionProvider.Cpu };
        switch (SelectedRuntime)
        {
            case "onnxruntime-builtin":
                if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    executionProviders.Add(OnnxExecutionProvider.CoreMl);
                break;
            case "onnxruntime-directml":
                executionProviders.Add(OnnxExecutionProvider.DirectMl);
                break;
            case "onnxruntime-nvidia":
                executionProviders.Add(OnnxExecutionProvider.Cuda);
                executionProviders.Add(OnnxExecutionProvider.TensorRt);
                break;
            case "onnxruntime-openvino":
                executionProviders.Add(OnnxExecutionProvider.OpenVino);
                break;
            case "onnxruntime-qnn":
                executionProviders.Add(OnnxExecutionProvider.Qnn);
                break;
        }

        return executionProviders.ToArray();
    }

    public void Initialize()
    {
        if (PlatformHelper.Platform is PlatformId.Wasm) return;

        try
        {
            // We don't use settings service here because it is not loaded at this state
            var selectedRuntime = ReadStringSetting(SettingSelectedRuntimeKey)?.Trim() ?? "no-runtime";
            
            var selectedRuntimeRoot = Path.Combine(_paths.OnnxRuntimesDirectory, selectedRuntime);

            if (TryLoadFromRoot(selectedRuntimeRoot))
            {
                SelectedRuntime = selectedRuntime;
                return;
            }

            SelectedRuntime = "onnxruntime-builtin";
            
            _logger.LogInformation("ONNX Runtime preload skipped");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize ONNX Runtime bootstrapper.");
        }
    }

    private bool TryLoadFromRoot(string rootPath)
    {
        if (!Directory.Exists(rootPath)) return false;

        foreach (var nativeDirectory in EnumerateNativeSearchDirectories(rootPath))
            if (TryLoadFromNativeDirectory(nativeDirectory))
                return true;

        return false;
    }

    private bool TryLoadFromNativeDirectory(string nativeDirectory)
    {
        if (!Directory.Exists(nativeDirectory)) return false;

        var fileNames = GetOnnxRuntimeFileNameCandidates();
        var providersShared = PlatformHelper.GetLibraryFileName("onnxruntime_providers_shared");

        // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        //     TrySetDllDirectory(nativeDirectory);

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
            _logger.LogInformation("Loaded ONNX Runtime from {Path}", fullPath);
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
}
