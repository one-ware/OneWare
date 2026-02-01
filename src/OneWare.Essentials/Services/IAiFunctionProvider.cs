using Microsoft.Extensions.AI;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IAiFunctionProvider
{
    event EventHandler<AiFunctionStartedEvent>? FunctionStarted;
    event EventHandler<AiFunctionCompletedEvent>? FunctionCompleted;
    ICollection<AIFunction> GetTools();
}
