using System;
using OneWare.Chat.ViewModels.ChatMessages;
using OneWare.Essentials.Models;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class ChatMessageSubAgentViewModelTests
{
    [Fact]
    public void Complete_CollapsesAndSummarizesSuccessfulRun()
    {
        var subAgent = new ChatMessageSubAgentViewModel("call_1", "explore");

        subAgent.Complete(new ChatSubAgentCompletedEvent("call_1", true)
        {
            Duration = TimeSpan.FromSeconds(12.5),
            TotalToolCalls = 8,
            TotalTokens = 12400
        });

        Assert.False(subAgent.IsRunning);
        Assert.True(subAgent.IsFinished);
        Assert.True(subAgent.IsSuccessful);
        Assert.False(subAgent.IsExpanded);
        Assert.Equal("Done · 12.5s · 8 tool calls · 12.4k tokens", subAgent.StatusText);
    }

    [Fact]
    public void Complete_ReportsFailureReason()
    {
        var subAgent = new ChatMessageSubAgentViewModel("call_1", "explore");

        subAgent.Complete(new ChatSubAgentCompletedEvent("call_1", false) { Error = "boom" });

        Assert.False(subAgent.IsSuccessful);
        Assert.Equal("Failed: boom", subAgent.StatusText);
    }

    [Fact]
    public void Complete_MarksCancelledRunAsUnsuccessful()
    {
        var subAgent = new ChatMessageSubAgentViewModel("call_1", "explore");

        subAgent.Complete(new ChatSubAgentCompletedEvent("call_1", true) { Cancelled = true });

        Assert.False(subAgent.IsSuccessful);
        Assert.Equal("Cancelled", subAgent.StatusText);
    }
}
