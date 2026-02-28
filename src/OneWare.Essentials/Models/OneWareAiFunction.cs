using Microsoft.Extensions.AI;

namespace OneWare.Essentials.Models;

public interface IOneWareAiFunction
{
    string Name { get; }

    string Description { get; }

    Delegate Handler { get; }

    string? FriendlyName { get; }

    bool RequirePermission { get; }

    bool RunOnUiThread { get; }

    string? PermissionScope { get; }

    string? PermissionQuestion { get; }

    string? PermissionDetail { get; }

    Func<AIFunctionArguments, string?>? PermissionDetailFactory { get; }
}

public sealed class OneWareAiFunction : IOneWareAiFunction
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required Delegate Handler { get; init; }

    public string? FriendlyName { get; init; }

    public bool RequirePermission { get; init; }

    public bool RunOnUiThread { get; init; }

    public string? PermissionScope { get; init; }

    public string? PermissionQuestion { get; init; }

    public string? PermissionDetail { get; init; }

    public Func<AIFunctionArguments, string?>? PermissionDetailFactory { get; init; }
}
