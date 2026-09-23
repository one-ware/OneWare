namespace OneWare.Essentials.Models;

public class AiFunctionEvent
{
    public required string Id { get; init; }
}

public class AiFunctionStartedEvent : AiFunctionEvent
{
    public required string FunctionName { get; init; }

    /// <summary>
    /// Name the function is registered with at the AI backend. <see cref="FunctionName"/> is the
    /// display name, which can differ, so this is what tool events of a chat service refer to.
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// Id the AI backend assigned to this tool call, when it provides one. Used to correlate the
    /// call with the tool events of the chat service, e.g. to show it inside the sub-agent that
    /// invoked it.
    /// </summary>
    public string? ToolCallId { get; init; }

    public string? Detail { get; init; }
}

public class AiFunctionCompletedEvent : AiFunctionEvent
{
    public required bool Result { get; init; }
    public string? ToolOutput { get; init; }
}

public class AiFunctionProgressEvent : AiFunctionEvent
{
    public required string Output { get; init; }
}
