using GitHub.Copilot.Rpc;

namespace OneWare.Copilot.Services;

/// <summary>A routing preference of the <c>auto</c> model as offered in the picker.</summary>
public sealed class CopilotAutoTierOption(string label, string? id, string? description = null, bool isFusion = false)
{
    public string Label { get; } = label;

    /// <summary>Runtime tier id, <c>null</c> to leave the routing to the provider.</summary>
    public string? Id { get; } = id;

    public string? Description { get; } = description;

    /// <summary>True for HydraFusion tiers, which combine several models per turn.</summary>
    public bool IsFusion { get; } = isFusion;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public override string ToString() => Label;
}

/// <summary>
/// Builds the Auto routing preferences. With <c>DYNAMIC_AUTO_TIERS</c> the runtime reports the tiers the account can
/// use (including HydraFusion), otherwise the built-in tiers apply.
/// </summary>
internal static class CopilotAutoTiers
{
    internal const string AutoType = "auto";
    internal const string FusionType = "fusion";
    private const string FastId = "fast";

    public static IReadOnlyList<CopilotAutoTierOption> Fallback { get; } =
    [
        new(CopilotChatServiceBase.AutoTierDefault, null, "Leave the choice to Copilot."),
        new(CopilotChatServiceBase.AutoTierFast, FastId, "Prefer the lowest-latency models."),
        new(CopilotChatServiceBase.AutoTierEfficiency, "efficiency", "Prefer cheaper models."),
        new(CopilotChatServiceBase.AutoTierBalance, "balance", "Trade off speed and capability."),
        new(CopilotChatServiceBase.AutoTierIntelligence, "intelligence", "Prefer the most capable models.")
    ];

    /// <summary>
    /// The tiers to offer for <paramref name="metadata"/>: enabled, picker-visible <c>auto</c> and <c>fusion</c>
    /// tiers in the provider's order. <c>fast</c> is hidden like in the Copilot CLI. Falls back to
    /// <see cref="Fallback"/> when the provider reported nothing usable.
    /// </summary>
    public static IReadOnlyList<CopilotAutoTierOption> Build(AutoTierMetadata? metadata)
    {
        if (metadata?.Tiers is not { Count: > 0 } tiers) return Fallback;

        var options = new List<CopilotAutoTierOption>();
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CopilotChatServiceBase.AutoTierDefault };
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tier in tiers)
        {
            if (string.IsNullOrWhiteSpace(tier.Id) || !ids.Add(tier.Id)) continue;
            if (string.Equals(tier.Id, FastId, StringComparison.OrdinalIgnoreCase)) continue;

            var isFusion = string.Equals(tier.Type, FusionType, StringComparison.OrdinalIgnoreCase);
            if (!isFusion && !string.Equals(tier.Type, AutoType, StringComparison.OrdinalIgnoreCase)) continue;
            if (tier.Status is { Enabled: false } || tier.Status?.PickerVisible == false) continue;

            var label = GetKnownLabel(tier.Id) ??
                        (string.IsNullOrWhiteSpace(tier.DisplayName) ? tier.Id : tier.DisplayName.Trim());
            if (!labels.Add(label))
            {
                label = tier.Id;
                if (!labels.Add(label)) continue;
            }

            options.Add(new CopilotAutoTierOption(label, tier.Id, tier.Description, isFusion));
        }

        if (options.Count == 0) return Fallback;

        var defaultLabel = Describe(options, metadata.DefaultTier);
        var defaultDescription = string.IsNullOrWhiteSpace(metadata.DefaultTier)
            ? "Leave the choice to Copilot."
            : $"Leave the choice to Copilot (currently {defaultLabel}).";

        options.Insert(0, new CopilotAutoTierOption(CopilotChatServiceBase.AutoTierDefault, null, defaultDescription));
        return options;
    }

    /// <summary>The runtime id of the tier labelled <paramref name="label"/>, <c>null</c> for Default or unknown labels.</summary>
    public static string? ResolveId(IEnumerable<CopilotAutoTierOption> options, string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        return options.FirstOrDefault(x => string.Equals(x.Label, label, StringComparison.Ordinal))?.Id;
    }

    /// <summary>The label for the runtime tier <paramref name="id"/>; <c>Default</c> when it is <c>null</c>.</summary>
    public static string Describe(IEnumerable<CopilotAutoTierOption> options, string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return CopilotChatServiceBase.AutoTierDefault;

        return options.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))?.Label
               ?? GetKnownLabel(id)
               ?? id;
    }

    private static string? GetKnownLabel(string id) =>
        Fallback.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))?.Label;
}
