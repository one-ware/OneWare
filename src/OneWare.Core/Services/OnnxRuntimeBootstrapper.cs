using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class OnnxRuntimeBootstrapper
{
    public const string SettingSelectedRuntimeKey = "OnnxRuntime_SelectedRuntime";
    
    public const string SettingSelectedExecutionProviderKey = "OnnxRuntime_SelectedExecutionProvider";
    
    public const string SettingOpenVinoDeviceKey = "OnnxRuntime_OpenVinoDevice";

    /// <summary>
    ///     Runtimes from the frozen combined-build packages. They ship an onnxruntime older than the
    ///     managed assembly, which is not ABI compatible, and were superseded by Windows ML and the
    ///     plugin execution providers. Stale installations are removed instead of being loaded.
    /// </summary>
    private static readonly string[] LegacyIncompatibleRuntimes =
    [
        "onnxruntime-directml",
        "onnxruntime-openvino",
        "onnxruntime-qnn"
    ];

    /// <summary>
    ///     Native libraries that live next to onnxruntime and are loaded dynamically by it at runtime.
    ///     They are preloaded by absolute path so the OS loader resolves them from the side-loaded
    ///     runtime directory instead of the application directory.
    /// </summary>
    private static readonly string[] SiblingDependencyLibraries =
    [
        "onnxruntime_providers_shared",
        "DirectML",
        "Microsoft.Windows.AI.MachineLearning"
    ];

    /// <summary>
    ///     Plugin execution providers are additive: they do not replace onnxruntime itself but are
    ///     registered against it via <see cref="OrtEnv.RegisterExecutionProviderLibrary"/>.
    ///     Maps the plugin library base name to its ONNX Runtime registration name.
    /// </summary>
    private static readonly (string LibraryBaseName, string RegistrationName)[] PluginExecutionProviders =
    [
        ("onnxruntime_providers_openvino_plugin", "OpenVINOExecutionProvider"),
        ("onnxruntime_providers_qnn", "QNNExecutionProvider")
    ];

    private readonly ILogger _logger;
    private readonly IPaths _paths;
    private static readonly Lock ResolverSync = new();
    private static string? _resolverNativeDirectory;
    private static IntPtr _resolverOnnxRuntimeHandle;
    private static bool _onnxResolverRegistered;

    public string SelectedRuntime { get; private set; } = "onnxruntime-builtin";

    /// <summary>
    ///     ONNX Runtime registration name of the side-loaded plugin execution provider, if the selected
    ///     runtime is a plugin execution provider rather than a full runtime.
    /// </summary>
    public string? PluginExecutionProviderName { get; private set; }

    /// <summary>
    ///     Absolute path to the side-loaded plugin execution provider library.
    /// </summary>
    public string? PluginExecutionProviderLibraryPath { get; private set; }

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
                    .Cast<string>()
                    .Where(x => !LegacyIncompatibleRuntimes.Contains(x, StringComparer.OrdinalIgnoreCase)));
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
        return GetOnnxExecutionProviders(SelectedRuntime);
    }

    public OnnxExecutionProvider[] GetOnnxExecutionProviders(string? runtimeName)
    {
        var executionProviders = new List<OnnxExecutionProvider> { OnnxExecutionProvider.Cpu };
        switch ((runtimeName ?? "onnxruntime-builtin").Trim())
        {
            case "onnxruntime-builtin":
                if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    executionProviders.Add(OnnxExecutionProvider.CoreMl);
                break;
            case "onnxruntime-windowsml":
                executionProviders.Add(OnnxExecutionProvider.DirectMl);
                break;
            case "onnxruntime-nvidia":
                executionProviders.Add(OnnxExecutionProvider.Cuda);
                executionProviders.Add(OnnxExecutionProvider.TensorRt);
                break;
            case "onnxruntime-ep-openvino":
                executionProviders.Add(OnnxExecutionProvider.OpenVino);
                break;
            case "onnxruntime-ep-qnn":
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

            RemoveLegacyRuntimes();

            if (LegacyIncompatibleRuntimes.Contains(selectedRuntime, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "ONNX runtime '{Runtime}' is no longer supported and was removed. Falling back to the built-in runtime.",
                    selectedRuntime);
                SelectedRuntime = "onnxruntime-builtin";
                return;
            }

            var selectedRuntimeRoot = Path.Combine(_paths.OnnxRuntimesDirectory, selectedRuntime);
            var runtimeRootToLoad = CreateSessionRuntimeCopy(selectedRuntime, selectedRuntimeRoot) ?? selectedRuntimeRoot;

            if (TryLoadFromRoot(runtimeRootToLoad))
            {
                SelectedRuntime = selectedRuntime;
                return;
            }

            // Keep a direct-load fallback if session staging failed for any reason.
            if (!string.Equals(runtimeRootToLoad, selectedRuntimeRoot, StringComparison.OrdinalIgnoreCase)
                && TryLoadFromRoot(selectedRuntimeRoot))
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

    /// <summary>
    ///     Deletes installations of runtimes that are no longer ABI compatible with the bundled managed
    ///     assembly, so they cannot be side-loaded and no longer show up as a selectable runtime.
    /// </summary>
    private void RemoveLegacyRuntimes()
    {
        foreach (var legacyRuntime in LegacyIncompatibleRuntimes)
        {
            var path = Path.Combine(_paths.OnnxRuntimesDirectory, legacyRuntime);
            if (!Directory.Exists(path)) continue;

            try
            {
                Directory.Delete(path, true);
                _logger.LogInformation("Removed unsupported ONNX runtime '{Runtime}'.", legacyRuntime);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to remove unsupported ONNX runtime '{Runtime}'.", legacyRuntime);
            }
        }
    }

    private string? CreateSessionRuntimeCopy(string runtimeName, string sourceRootPath)
    {
        if (string.IsNullOrWhiteSpace(runtimeName) || !Directory.Exists(sourceRootPath))
            return null;

        var sessionRootPath = Path.Combine(_paths.SessionDirectory, "OnnxRuntimes", runtimeName);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sessionRootPath)!);
            if (Directory.Exists(sessionRootPath))
                Directory.Delete(sessionRootPath, true);

            PlatformHelper.CopyDirectory(sourceRootPath, sessionRootPath);
            return sessionRootPath;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to copy ONNX runtime '{RuntimeName}' into session directory.", runtimeName);
            return null;
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

        PreloadSiblingDependencies(nativeDirectory);

        foreach (var fileName in GetOnnxRuntimeFileNameCandidates().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.Combine(nativeDirectory, fileName);
            if (!File.Exists(fullPath)) continue;

            if (!NativeLibrary.TryLoad(fullPath, out var handle)) continue;

            ConfigureOnnxRuntimeDllImportResolver(nativeDirectory, handle);
            _logger.LogInformation("Loaded ONNX Runtime from {Path}", fullPath);
            return true;
        }

        return TryDetectPluginExecutionProvider(nativeDirectory);
    }

    /// <summary>
    ///     Detects a plugin execution provider in the given directory. Plugin providers ship without
    ///     onnxruntime itself and are registered against the active runtime once ONNX Runtime is
    ///     initialized, so nothing is loaded here beyond recording the library path.
    /// </summary>
    private bool TryDetectPluginExecutionProvider(string nativeDirectory)
    {
        foreach (var (libraryBaseName, registrationName) in PluginExecutionProviders)
        {
            foreach (var candidate in BuildLibraryFileCandidates(libraryBaseName))
            {
                var fullPath = Path.Combine(nativeDirectory, candidate);
                if (!File.Exists(fullPath)) continue;

                PluginExecutionProviderName = registrationName;
                PluginExecutionProviderLibraryPath = fullPath;

                // Preloading by absolute path lets the loader resolve the provider's own dependencies
                // (OpenVINO/QNN runtime libraries) from the side-loaded directory.
                if (!NativeLibrary.TryLoad(fullPath, out _))
                    _logger.LogDebug("Failed to preload plugin execution provider '{Path}'.", fullPath);

                _logger.LogInformation("Found ONNX Runtime plugin execution provider {Provider} at {Path}",
                    registrationName, fullPath);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Preloads native libraries that sit next to onnxruntime and are resolved dynamically by it.
    ///     Loading them by absolute path first makes the loader reuse the already loaded module instead
    ///     of searching the application directory, where the side-loaded copies do not exist.
    /// </summary>
    private void PreloadSiblingDependencies(string nativeDirectory)
    {
        foreach (var libraryBaseName in SiblingDependencyLibraries)
        {
            // Not using BuildLibraryFileCandidates: names like "Microsoft.Windows.AI.MachineLearning"
            // already contain dots and would be treated as having a file extension.
            var platformFileName = PlatformHelper.GetLibraryFileName(libraryBaseName);

            foreach (var candidate in new[] { platformFileName, $"lib{platformFileName}" })
            {
                var fullPath = Path.Combine(nativeDirectory, candidate);
                if (!File.Exists(fullPath)) continue;

                if (!NativeLibrary.TryLoad(fullPath, out _))
                    _logger.LogDebug("Failed to preload ONNX Runtime dependency '{Path}'.", fullPath);
            }
        }
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
                directories.AddRange(
                    Directory.EnumerateDirectories(runtimesRoot)
                        .Select(path => new
                        {
                            Name = Path.GetFileName(path),
                            NativePath = Path.Combine(path, "native")
                        })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                        .Where(x => x.Name.Equals(PlatformHelper.PlatformIdentifier, StringComparison.OrdinalIgnoreCase))
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
                // Our resolver takes over native lookup, so ONNX Runtime must not install its own.
                OrtEnv.DisableDllImportResolver = true;

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
