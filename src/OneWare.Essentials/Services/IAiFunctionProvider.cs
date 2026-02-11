using Microsoft.Extensions.AI;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IAiFunctionProvider
{
    /// <summary>
    /// Fired when an AI function starts.
    /// </summary>
    event EventHandler<AiFunctionStartedEvent>? FunctionStarted;
    /// <summary>
    /// Fired when an AI function completes.
    /// </summary>
    event EventHandler<AiFunctionCompletedEvent>? FunctionCompleted;
    /// <summary>
    /// Returns available AI tools for this provider.
    /// </summary>
    ICollection<AIFunction> GetTools();
}
