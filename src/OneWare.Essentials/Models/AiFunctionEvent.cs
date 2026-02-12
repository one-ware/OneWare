namespace OneWare.Essentials.Models;

public enum AiFunctionPermissionDecision
{
    Deny,
    AllowOnce,
    AllowForSession
}

public class AiFunctionEvent
{
    public required string Id { get; init; }
}

public class AiFunctionStartedEvent : AiFunctionEvent
{
    public required string FunctionName { get; init; }
        
    public string? Detail { get; init; }
}

public class AiFunctionPermissionRequestEvent : AiFunctionEvent
{
    public required string FunctionName { get; init; }

    public required string Question { get; init; }

    public string? Detail { get; init; }

    public required TaskCompletionSource<AiFunctionPermissionDecision> DecisionSource { get; init; }
}

public class AiFunctionCompletedEvent : AiFunctionEvent
{
    public required bool Result { get; init; }
    
    public string? ToolOutput { get; init; }
}
