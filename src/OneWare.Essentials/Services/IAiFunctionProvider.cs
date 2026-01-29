using Microsoft.Extensions.AI;

namespace OneWare.Essentials.Services;

public interface IAiFunctionProvider
{
    event EventHandler<string>? FunctionUsed;
    ICollection<AIFunction> GetTools();
}
