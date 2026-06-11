using Microsoft.Extensions.AI;

namespace OneWare.Essentials.Models;

public interface IOneWareAiFunction
{
    string Name { get; }
    string Description { get; }
    Delegate Handler { get; }
    string? FriendlyName { get; }
    bool RunOnUiThread { get; }
    /// <summary>
    /// Optional delegate called with the actual invocation arguments to decide whether the user
    /// must confirm before the function executes.
    /// Return <see langword="null"/> to proceed without a prompt;
    /// return a non-empty string (the reason shown to the user) to request confirmation via
    /// <see cref="SessionHooks.OnPreToolUse"/> returning <c>"ask"</c>.
    /// </summary>
    Func<AIFunctionArguments, string?>? ConfirmationCheck { get; }
}

public sealed class OneWareAiFunction : IOneWareAiFunction
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Delegate Handler { get; init; }
    public string? FriendlyName { get; init; }
    public bool RunOnUiThread { get; init; }
    /// <inheritdoc />
    public Func<AIFunctionArguments, string?>? ConfirmationCheck { get; init; }
}
