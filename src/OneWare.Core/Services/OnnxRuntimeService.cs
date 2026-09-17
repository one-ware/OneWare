using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class OnnxRuntimeService : IOnnxRuntimeService
{
    private static readonly HashSet<string> OpenVinoDevices = ["GPU", "NPU", "CPU"];

    private readonly ISettingsService _settingsService;
    private readonly OnnxRuntimeBootstrapper _bootstrapper;
    private readonly ILogger _logger;
    private readonly Lock _pluginRegistrationSync = new();
    private bool _pluginRegistrationAttempted;
    private bool _pluginRegistered;

    public OnnxRuntimeService(OnnxRuntimeBootstrapper bootstrapper, ILogger logger, ISettingsService settingsService)
    {
        _bootstrapper = bootstrapper;
        _logger = logger;
        _settingsService = settingsService;
    }

    public OnnxExecutionProvider PreferredExecutionProvider =>
        _settingsService.GetSettingValue<OnnxExecutionProvider>(OnnxRuntimeBootstrapper
            .SettingSelectedExecutionProviderKey);

    public OnnxExecutionProvider[] GetAvailableExecutionProviders()
    {
        return _bootstrapper.GetOnnxExecutionProviders();
    }

    public SessionOptions CreateSessionOptions(OnnxExecutionProvider? providerOverride = null)
    {
        var provider = providerOverride ?? PreferredExecutionProvider;

        if (!GetAvailableExecutionProviders().Contains(provider))
        {
            _logger.Warning($"Wanted execution provider {provider} not supported by this runtime / os. Falling back to CPU");
            provider = OnnxExecutionProvider.Cpu;
        }
        
        var so = new SessionOptions
        {
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,

            // Start here for compatibility; move to ORT_ENABLE_ALL once validated.
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,

            EnableCpuMemArena = true,
            EnableMemoryPattern = true
        };
        
        // Threads: good general defaults (tune per app)
        so.IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount);
        so.InterOpNumThreads = 1;

        try
        {
            switch (provider)
            {
                case OnnxExecutionProvider.Cuda:
                {
                    var cudaOpts = new OrtCUDAProviderOptions();

                    var dict = new Dictionary<string, string>
                    {
                        // Pick the NVIDIA GPU you want (0 = first NVIDIA device per CUDA)
                        ["device_id"] = "0",

                        // Usually best startup/perf tradeoff:
                        ["cudnn_conv_algo_search"] = "HEURISTIC",

                        // Allow cuDNN to use more workspace for better perf:
                        ["cudnn_conv_use_max_workspace"] = "1",
                        
                        // Optional: can help stream behavior in some apps:
                        ["do_copy_in_default_stream"] = "1"
                    };

                    cudaOpts.UpdateOptions(dict);

                    // Important: MakeSessionOptionWithCudaProvider returns a new instance,
                    // so re-apply base options AFTER it.
                    so.Dispose();
                    so = SessionOptions.MakeSessionOptionWithCudaProvider(cudaOpts);

                    // Re-apply your baseline settings
                    so.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                    so.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
                    so.EnableCpuMemArena = true;
                    so.EnableMemoryPattern = true;
                    so.IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount);
                    so.InterOpNumThreads = 1;

                    break;
                }

                case OnnxExecutionProvider.DirectMl:
                    so.AppendExecutionProvider_DML(0);
                    so.EnableMemoryPattern = false;
                    break;
                
                case OnnxExecutionProvider.TensorRt:
                    so.AppendExecutionProvider_Tensorrt();
                    break;
                
                case OnnxExecutionProvider.CoreMl:
                    so.AppendExecutionProvider_CoreML();
                    so.EnableMemoryPattern = false;
                    break;
                
                case OnnxExecutionProvider.OpenVino:
                    if (!TryAppendPluginExecutionProvider(so, "OpenVINOExecutionProvider",
                            new Dictionary<string, string> { ["device_type"] = GetOpenVinoDevice() }))
                        so.AppendExecutionProvider_OpenVINO(GetOpenVinoDevice());
                    break;

                case OnnxExecutionProvider.Qnn:
                    if (!TryAppendPluginExecutionProvider(so, "QNNExecutionProvider",
                            new Dictionary<string, string>()))
                        so.AppendExecutionProvider("QNN");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure ONNX Runtime provider '{Provider}'.", provider.ToString());
        }
        
        _logger.LogInformation("Created ONNX Runtime provider '{Provider}'.", provider.ToString());

        return so;
    }

    /// <summary>
    ///     Registers a side-loaded plugin execution provider with ONNX Runtime and appends the devices it
    ///     exposes to the session options. Plugin providers replaced the frozen combined-build packages
    ///     (OpenVINO, QNN) and are additive to the active runtime.
    /// </summary>
    private bool TryAppendPluginExecutionProvider(SessionOptions sessionOptions, string providerName,
        Dictionary<string, string> providerOptions)
    {
        if (!string.Equals(_bootstrapper.PluginExecutionProviderName, providerName, StringComparison.Ordinal))
            return false;

        if (!EnsurePluginExecutionProviderRegistered()) return false;

        var env = OrtEnv.Instance();
        var devices = env.GetEpDevices()
            .Where(x => string.Equals(x.EpName, providerName, StringComparison.Ordinal))
            .ToArray();

        if (devices.Length == 0)
        {
            _logger.LogWarning("Plugin execution provider '{Provider}' did not expose any devices.", providerName);
            return false;
        }

        sessionOptions.AppendExecutionProvider(env, devices, providerOptions);
        return true;
    }

    private bool EnsurePluginExecutionProviderRegistered()
    {
        lock (_pluginRegistrationSync)
        {
            if (_pluginRegistrationAttempted) return _pluginRegistered;
            _pluginRegistrationAttempted = true;

            var name = _bootstrapper.PluginExecutionProviderName;
            var path = _bootstrapper.PluginExecutionProviderLibraryPath;
            if (name == null || path == null) return false;

            try
            {
                OrtEnv.Instance().RegisterExecutionProviderLibrary(name, path);
                _pluginRegistered = true;
                _logger.LogInformation("Registered ONNX Runtime plugin execution provider '{Provider}'.", name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register ONNX Runtime plugin execution provider '{Provider}'.", name);
            }

            return _pluginRegistered;
        }
    }

    private string GetOpenVinoDevice()
    {
        var configuredDevice = _settingsService
            .GetSettingValue<string>(OnnxRuntimeBootstrapper.SettingOpenVinoDeviceKey)
            .Trim()
            .ToUpperInvariant();

        if (OpenVinoDevices.Contains(configuredDevice))
            return configuredDevice;
        
        _logger.Warning($"Invalid OpenVINO device setting '{configuredDevice}'. Falling back to GPU.");
        return "GPU";
    }
}
