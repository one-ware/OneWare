using System;
using OneWare.Chat.ViewModels;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class ChatActivityStatusTests
{
    [Fact]
    public void GetText_FollowsTheModelPhase()
    {
        var status = new ChatActivityStatus();
        Assert.Equal("Working…", status.GetText());

        status.Phase = ChatActivityPhase.Thinking;
        Assert.Equal("Thinking…", status.GetText());

        status.Phase = ChatActivityPhase.Writing;
        Assert.Equal("Writing…", status.GetText());
    }

    [Fact]
    public void GetText_ShowsRunningToolsAndReturnsToWorkingAfterwards()
    {
        var status = new ChatActivityStatus { Phase = ChatActivityPhase.Thinking };

        status.ToolStarted("a", "Execute In Terminal");
        Assert.Equal("Running Execute In Terminal…", status.GetText());

        status.ToolStarted("b", "Read File");
        Assert.Equal("Running 2 tools…", status.GetText());

        status.ToolCompleted("a");
        Assert.Equal("Running Read File…", status.GetText());

        status.ToolCompleted("b");
        Assert.Equal("Working…", status.GetText());
    }

    [Fact]
    public void GetText_PrefersPromptsOverSteeringOverToolsOverSubAgents()
    {
        var status = new ChatActivityStatus();
        var prompt = new object();

        status.SubAgentStarted("agent", "Explorer");
        Assert.Equal("Waiting for Explorer…", status.GetText());

        status.ToolStarted("tool", "Edit File");
        Assert.Equal("Running Edit File…", status.GetText());

        status.IsSteering = true;
        Assert.Equal("Steering…", status.GetText());

        status.PromptOpened(prompt, "Waiting for approval…");
        Assert.Equal("Waiting for approval…", status.GetText());

        status.PromptClosed(prompt);
        status.IsSteering = false;
        status.ToolCompleted("tool");
        status.SubAgentCompleted("agent");
        Assert.Equal("Working…", status.GetText());
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        var status = new ChatActivityStatus { Phase = ChatActivityPhase.Writing, IsSteering = true };
        status.ToolStarted("tool", "Edit File");
        status.PromptOpened(new object(), "Waiting for approval…");

        status.Reset();

        Assert.Equal("Working…", status.GetText());
    }
}
