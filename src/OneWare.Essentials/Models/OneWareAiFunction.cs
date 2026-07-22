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

    /// <summary>
    /// Optional delegate that extracts a short human-readable detail (e.g. a relative file path)
    /// from the invocation arguments. The returned string is shown in the chat tool message
    /// while the tool is running.
    /// </summary>
    Func<AIFunctionArguments, string?>? DetailExtractor { get; }

    /// <summary>
    /// Optional invocation handler for tools that need access to invocation-scoped services such
    /// as progress reporting. <see cref="Handler"/> is still used to generate the AI tool schema.
    /// </summary>
    Func<AiFunctionInvocationContext, AIFunctionArguments, CancellationToken, ValueTask<object?>>?
        InvocationHandler => null;
}

public sealed class AiFunctionInvocationContext(string id, Action<string> reportProgress)
{
    public string Id { get; } = id;

    public void ReportProgress(string output) => reportProgress(output);
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
    /// <inheritdoc />
    public Func<AIFunctionArguments, string?>? DetailExtractor { get; init; }
    /// <inheritdoc />
    public Func<AiFunctionInvocationContext, AIFunctionArguments, CancellationToken, ValueTask<object?>>?
        InvocationHandler { get; init; }
}
