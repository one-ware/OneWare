namespace OneWare.Essentials.Models;

public class AiFunctionEvent
{
    public required string Id { get; init; }
}

public class AiFunctionStartedEvent : AiFunctionEvent
{
    public required string FunctionName { get; init; }
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

/// <summary>
/// Fired when a running AI function attaches a live view model to its chat tool box, e.g. the
/// mini terminal of <c>runTerminalCommand</c>. The chat renders <see cref="Content"/> through
/// the application's view locator, so any view model with a matching view can be used.
/// </summary>
public class AiFunctionContentEvent : AiFunctionEvent
{
    public required object? Content { get; init; }
}
