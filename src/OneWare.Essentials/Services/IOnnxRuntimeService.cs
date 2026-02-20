using Microsoft.ML.OnnxRuntime;

namespace OneWare.Essentials.Services;

public interface IOnnxRuntimeService
{
    public string SelectedRuntime { get; }

    public string SelectedExecutionProvider { get; }

    public SessionOptions CreateSessionOptions();
}
