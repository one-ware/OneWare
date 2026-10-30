using GitHub.Copilot;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

/// <summary>
/// HydraFusion turns (Auto tiers of type <c>fusion</c>) run several models per turn. Phases that work in the root
/// conversation (primary, revision, …) produce the answer and stay in the main flow. Review phases (critic, judge)
/// run in an isolated history and are shown in their own collapsible block, like a sub-agent.
/// </summary>
public abstract partial class CopilotChatServiceBase
{
    private const string FusionBlockPrefix = "fusion:";

    // Running review phases by phase id.
    private readonly Dictionary<string, FusionReviewPhase> _fusionPhases = new(StringComparer.Ordinal);

    // The router's short summary of a turn's plan, by fusion id.
    private readonly Dictionary<string, string> _fusionHints = new(StringComparer.Ordinal);

    private sealed class FusionReviewPhase(string blockId, string fusionId)
    {
        public string BlockId { get; } = blockId;

        public string FusionId { get; } = fusionId;

        /// <summary>True once the phase's own messages were shown in its block.</summary>
        public bool HasContent { get; set; }
    }

    private static bool IsFusionReviewScope(FusionConversationScope scope) =>
        string.Equals(scope.Value, FusionConversationScope.Review.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>Handles the HydraFusion lifecycle events; returns false for every other event.</summary>
    private bool HandleFusionEvent(SessionEvent evt)
    {
        switch (evt)
        {
            case SessionFusionResolvedEvent x:
                lock (_fusionPhases)
                {
                    if (!string.IsNullOrWhiteSpace(x.Data.FusionId) && !string.IsNullOrWhiteSpace(x.Data.Hint))
                        _fusionHints[x.Data.FusionId] = x.Data.Hint;
                }

                ContainerLocator.Container.Resolve<ILogger>().LogInformation(
                    "Copilot Fusion: {Pattern} with {Primary} ({Hint})", x.Data.Pattern.Value,
                    x.Data.PrimaryModel, x.Data.Hint);
                return true;
            case SessionFusionRouteFailedEvent x:
                ContainerLocator.Container.Resolve<ILogger>().LogWarning(
                    "Copilot Fusion routing failed ({Reason}): {Error}. Falling back to {Model}.",
                    x.Data.Reason, x.Data.ErrorMessage, x.Data.FallbackModel);
                return true;
            case AssistantFusionPhaseStartedEvent x:
                if (IsFusionReviewScope(x.Data.ConversationScope)) StartFusionPhase(x.Data);
                return true;
            case AssistantFusionPhaseCompletedEvent x:
                CompleteFusionPhase(x.Data);
                return true;
            case AssistantFusionPhaseFailedEvent x:
                FailFusionPhase(x.Data);
                return true;
            case SessionFusionCompletedEvent x:
                ContainerLocator.Container.Resolve<ILogger>().LogInformation(
                    "Copilot Fusion finished: {Outcome}, {Phases} phases, {Requests} requests in {Duration:0} ms{Degraded}",
                    x.Data.Outcome, x.Data.PhaseCount, x.Data.RequestCount, x.Data.DurationMs,
                    string.IsNullOrWhiteSpace(x.Data.DegradedReason) ? "" : $" (degraded: {x.Data.DegradedReason})");
                CloseFusionPhases(x.Data.FusionId);
                return true;
            case AssistantFusionPhaseActivityEvent or SessionFusionRouteStartedEvent:
                return true;
            default:
                return false;
        }
    }

    private void StartFusionPhase(AssistantFusionPhaseStartedData data)
    {
        if (string.IsNullOrWhiteSpace(data.PhaseId)) return;

        var blockId = FusionBlockPrefix + data.PhaseId;
        string? hint;
        lock (_fusionPhases)
        {
            if (_fusionPhases.ContainsKey(data.PhaseId)) return;
            _fusionPhases[data.PhaseId] = new FusionReviewPhase(blockId, data.FusionId);
            _fusionHints.TryGetValue(data.FusionId ?? "", out hint);
        }

        EventReceived?.Invoke(this, new ChatSubAgentStartedEvent(blockId, $"Fusion {DescribeFusionPhase(data.PhaseKind)}")
        {
            Description = FirstNonEmpty(data.Role, hint),
            Model = DescribeModel(data.Model)
        });
    }

    private void CompleteFusionPhase(AssistantFusionPhaseCompletedData data)
    {
        var phase = RemoveFusionPhase(data.PhaseId);
        if (phase == null) return;

        // Review output is often only reported here, not as messages of its own.
        var text = phase.HasContent ? null : data.Content?.Trim();
        if (!string.IsNullOrWhiteSpace(data.Verdict))
            text = string.IsNullOrWhiteSpace(text)
                ? $"**Verdict:** {data.Verdict}"
                : $"**Verdict:** {data.Verdict}\n\n{text}";

        if (!string.IsNullOrWhiteSpace(text))
            EventReceived?.Invoke(this, new ChatMessageEvent(text) { AgentId = phase.BlockId, Model = DescribeModel(data.Model) });

        var status = data.Status.Value;
        EventReceived?.Invoke(this, new ChatSubAgentCompletedEvent(phase.BlockId,
            !string.Equals(status, FusionPhaseStatus.Failed.Value, StringComparison.OrdinalIgnoreCase))
        {
            Cancelled = string.Equals(status, FusionPhaseStatus.Cancelled.Value, StringComparison.OrdinalIgnoreCase),
            Duration = TimeSpan.FromMilliseconds(data.DurationMs),
            TotalTokens = data.Usage is { } usage ? usage.InputTokens + usage.OutputTokens : null
        });
    }

    private void FailFusionPhase(AssistantFusionPhaseFailedData data)
    {
        ContainerLocator.Container.Resolve<ILogger>().LogWarning(
            "Copilot Fusion phase {Phase} ({Model}) failed ({Reason}): {Error}", data.PhaseKind.Value, data.Model,
            data.Reason, data.ErrorMessage);

        var phase = RemoveFusionPhase(data.PhaseId);
        if (phase == null) return;

        EventReceived?.Invoke(this, new ChatSubAgentCompletedEvent(phase.BlockId, false)
        {
            Error = FirstNonEmpty(data.ErrorMessage, data.Reason),
            Cancelled = string.Equals(data.Status.Value, FusionPhaseStatus.Cancelled.Value,
                StringComparison.OrdinalIgnoreCase),
            Duration = TimeSpan.FromMilliseconds(data.DurationMs)
        });
    }

    private FusionReviewPhase? RemoveFusionPhase(string? phaseId)
    {
        if (string.IsNullOrWhiteSpace(phaseId)) return null;

        lock (_fusionPhases)
            return _fusionPhases.Remove(phaseId, out var phase) ? phase : null;
    }

    /// <summary>
    /// Closes review blocks that never reported an end, for the turn <paramref name="fusionId"/> or, when
    /// <c>null</c>, for every turn (session idle).
    /// </summary>
    private void CloseFusionPhases(string? fusionId)
    {
        List<FusionReviewPhase> open;
        lock (_fusionPhases)
        {
            open = _fusionPhases.Values
                .Where(x => fusionId == null || string.Equals(x.FusionId, fusionId, StringComparison.Ordinal))
                .ToList();
            foreach (var phase in _fusionPhases.Where(x => open.Contains(x.Value)).Select(x => x.Key).ToArray())
                _fusionPhases.Remove(phase);

            if (fusionId == null) _fusionHints.Clear();
            else _fusionHints.Remove(fusionId);
        }

        foreach (var phase in open)
            EventReceived?.Invoke(this, new ChatSubAgentCompletedEvent(phase.BlockId, true));
    }

    /// <summary>
    /// The block of the review phase <paramref name="evt"/> belongs to, or <c>null</c> when it belongs to the main
    /// flow. Messages and tools name their phase; streaming deltas don't, so they go to the review phase when exactly
    /// one is running.
    /// </summary>
    private string? ResolveFusionAgentId(SessionEvent evt)
    {
        lock (_fusionPhases)
        {
            if (_fusionPhases.Count == 0) return null;

            var attribution = evt switch
            {
                AssistantMessageEvent x => x.Data.Fusion,
                ToolExecutionStartEvent x => x.Data.Fusion,
                ToolExecutionCompleteEvent x => x.Data.Fusion,
                _ => null
            };

            FusionReviewPhase? phase;
            if (attribution != null)
            {
                if (attribution.PhaseId == null || !_fusionPhases.TryGetValue(attribution.PhaseId, out phase))
                    return null;
            }
            else if (evt is AssistantMessageDeltaEvent or AssistantReasoningDeltaEvent or AssistantReasoningEvent
                     && _fusionPhases.Count == 1)
            {
                phase = _fusionPhases.Values.First();
            }
            else
            {
                return null;
            }

            if (evt is AssistantMessageEvent or AssistantMessageDeltaEvent) phase.HasContent = true;
            return phase.BlockId;
        }
    }

    private static string DescribeFusionPhase(FusionPhaseKind kind)
    {
        var value = kind.Value;
        if (string.IsNullOrWhiteSpace(value)) return "Review";

        var words = value.Replace('_', ' ').Replace('-', ' ').Trim();
        return char.ToUpperInvariant(words[0]) + words[1..];
    }
}
