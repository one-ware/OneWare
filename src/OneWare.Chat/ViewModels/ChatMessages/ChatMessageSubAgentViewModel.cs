using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;

namespace OneWare.Chat.ViewModels.ChatMessages;

/// <summary>
/// A block for work the AI delegated to a sub-agent. Everything the sub-agent does (its messages,
/// reasoning and tool calls) is nested inside <see cref="Items"/> instead of being mixed into the
/// main conversation, so a delegated task reads as one collapsible unit.
/// </summary>
public class ChatMessageSubAgentViewModel : ObservableObject, IChatMessage, IEstimatedHeightItem
{
    public ChatMessageSubAgentViewModel(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
        Timestamp = DateTimeOffset.Now;
        StatusText = "Starting…";
    }

    public string Id { get; }

    [DataMember] public string DisplayName { get; }

    [DataMember]
    public string? Description
    {
        get;
        set => SetProperty(ref field, value);
    }

    [DataMember]
    public string? Model
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) OnPropertyChanged(nameof(HasModel));
        }
    }

    public bool HasModel => !string.IsNullOrWhiteSpace(Model);

    [DataMember]
    public bool IsBackground
    {
        get;
        set => SetProperty(ref field, value);
    }

    public DateTimeOffset Timestamp { get; }

    public ObservableCollection<IChatMessage> Items { get; } = [];

    public bool IsRunning
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) OnPropertyChanged(nameof(IsFinished));
        }
    } = true;

    public bool IsFinished => !IsRunning;

    [DataMember]
    public bool IsSuccessful
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    /// <summary>What the sub-agent is doing right now, or how it ended.</summary>
    [DataMember]
    public string StatusText
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>Expanded while the sub-agent works, collapsed to its summary once it is done.</summary>
    public bool IsExpanded
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public void Complete(ChatSubAgentCompletedEvent completed)
    {
        IsRunning = false;
        IsSuccessful = completed.Success && !completed.Cancelled;
        IsExpanded = false;
        StatusText = BuildSummary(completed);
    }

    private static string BuildSummary(ChatSubAgentCompletedEvent completed)
    {
        if (!completed.Success)
            return string.IsNullOrWhiteSpace(completed.Error) ? "Failed" : $"Failed: {completed.Error}";

        var parts = new List<string> { completed.Cancelled ? "Cancelled" : "Done" };

        if (completed.Duration is { } duration && duration > TimeSpan.Zero)
            parts.Add(duration.TotalSeconds < 60
                ? $"{duration.TotalSeconds:0.#}s"
                : $"{(int)duration.TotalMinutes}m {duration.Seconds}s");

        if (completed.TotalToolCalls is > 0)
            parts.Add($"{completed.TotalToolCalls} tool calls");

        if (completed.TotalTokens is > 0)
            parts.Add($"{FormatTokens(completed.TotalTokens.Value)} tokens");

        return string.Join(" · ", parts);
    }

    private static string FormatTokens(long tokens)
    {
        return tokens >= 1000 ? $"{tokens / 1000d:0.#}k" : tokens.ToString();
    }

    public double EstimateHeight(double width)
    {
        const double header = 40;
        if (!IsExpanded) return header;

        var height = header;
        foreach (var item in Items)
            height += item is IEstimatedHeightItem estimated ? estimated.EstimateHeight(width - 20) : 36;

        return height + 8;
    }
}
