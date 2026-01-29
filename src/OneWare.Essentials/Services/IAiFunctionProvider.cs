using Microsoft.Extensions.AI;

namespace OneWare.Essentials.Services;

public interface IAiFunctionProvider
{
    event EventHandler<string>? FunctionStarted;
    event EventHandler<string>? FunctionCompleted;
    ICollection<AIFunction> GetTools();
}
