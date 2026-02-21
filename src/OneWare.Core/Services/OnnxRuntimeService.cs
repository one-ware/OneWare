using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class OnnxRuntimeService : IOnnxRuntimeService
{
    private const string RuntimeNvidia = "onnxruntime-nvidia";
    private const string RuntimeDirectMl = "onnxruntime-directml";

    private readonly OnnxRuntimeBootstrapper _bootstrapper;
    private readonly ILogger _logger;

    public OnnxRuntimeService(OnnxRuntimeBootstrapper bootstrapper, ILogger logger)
    {
        _bootstrapper = bootstrapper;
        _logger = logger;
    }

    public string SelectedRuntime => _bootstrapper.SelectedRuntime;

    // TODO we can add another setting to allow TensorRT instead of cuda
    public string SelectedExecutionProvider => SelectedRuntime switch
    {
        RuntimeNvidia => "cuda",
        RuntimeDirectMl => "directml",
        _ => "cpu"
    };

    public SessionOptions CreateSessionOptions()
    {
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
            switch (SelectedExecutionProvider)
            {
                case "cuda":
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

                        // Optional: set a cap (bytes). Example: 8 GiB:
                        // ["gpu_mem_limit"] = (8L * 1024 * 1024 * 1024).ToString(),

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

                case "directml":
                    so.AppendExecutionProvider_DML(0);
                    so.EnableMemoryPattern = false; // recommended for DML
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure ONNX Runtime provider '{Provider}'.", SelectedExecutionProvider);
        }
        
        _logger.LogInformation("Created ONNX Runtime provider '{Provider}'.", SelectedExecutionProvider);

        return so;
    }
}