using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class OnnxRuntimeService : IOnnxRuntimeService
{
    private const string RuntimeCpu = "onnxruntime-cpu";
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

    public string SelectedExecutionProvider { get; private set; } = "cpu";

    public SessionOptions CreateSessionOptions()
    {
        var sessionOptions = new SessionOptions();

        sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        sessionOptions.EnableCpuMemArena = true;

        var preferredProvider = SelectedRuntime switch
        {
            RuntimeNvidia => "cuda",
            RuntimeDirectMl => "directml",
            _ => "cpu"
        };

        if (preferredProvider != "cpu" && TryConfigureProvider(sessionOptions, preferredProvider))
        {
            SelectedExecutionProvider = preferredProvider;
            return sessionOptions;
        }

        SelectedExecutionProvider = "cpu";
        return sessionOptions;
    }

    private bool TryConfigureProvider(SessionOptions sessionOptions, string provider)
    {
        if (provider == "cpu") return true;

        var methodName = provider switch
        {
            "cuda" => "AppendExecutionProvider_CUDA",
            "directml" => "AppendExecutionProvider_DML",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(methodName)) return false;

        try
        {
            var methods = typeof(SessionOptions).GetMethods()
                .Where(x => x.Name.Equals(methodName, StringComparison.Ordinal));

            foreach (var method in methods)
            {
                var args = BuildArgs(method.GetParameters());
                if (args == null) continue;

                method.Invoke(sessionOptions, args);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to configure ONNX Runtime provider '{Provider}'.", provider);
        }

        return false;
    }

    private static object[]? BuildArgs(ParameterInfo[] parameters)
    {
        if (parameters.Length == 0) return [];

        var args = new object[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.HasDefaultValue)
            {
                args[i] = parameter.DefaultValue ?? GetDefaultValue(parameter.ParameterType)!;
                continue;
            }

            if (parameter.ParameterType == typeof(int))
            {
                args[i] = 0;
                continue;
            }

            if (parameter.ParameterType == typeof(long))
            {
                args[i] = 0L;
                continue;
            }

            if (parameter.ParameterType == typeof(bool))
            {
                args[i] = false;
                continue;
            }

            return null;
        }

        return args;
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
