using Microsoft.ML.OnnxRuntime;
using OneWare.Essentials.Enums;

namespace OneWare.Essentials.Services;

public interface IOnnxRuntimeService
{
    public OnnxExecutionProvider PreferredExecutionProvider { get; }
    
    public OnnxExecutionProvider[] GetAvailableExecutionProviders();

    public SessionOptions CreateSessionOptions(OnnxExecutionProvider? providerOverride = null);
}
