using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Services.Ai;

/// <summary>
/// Turns exceptions raised inside an FPGA chat function into a message the model can act on.
/// </summary>
/// <remarks>
/// A thrown exception aborts the tool call and gives the model nothing to work with. Returning the
/// reason as text lets it correct the call — a wrong toolchain id, a project that is not active —
/// without a round trip through the user.
/// </remarks>
public static class FpgaFunctionGuard
{
    /// <summary>
    /// Runs <paramref name="action"/> and converts failures into a readable reply.
    /// </summary>
    public static string Run(Func<string> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return action();
        }
        catch (FpgaAgentException e)
        {
            return $"Error: {e.Message}";
        }
        catch (OperationCanceledException)
        {
            return "Cancelled: the tool stopped before it completed.";
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return $"Error: {e.Message}";
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> and converts failures into a readable reply.
    /// </summary>
    public static async Task<string> RunAsync(Func<Task<string>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return await action();
        }
        catch (FpgaAgentException e)
        {
            return $"Error: {e.Message}";
        }
        catch (OperationCanceledException)
        {
            return "Cancelled: the tool stopped before it completed.";
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return $"Error: {e.Message}";
        }
    }
}
