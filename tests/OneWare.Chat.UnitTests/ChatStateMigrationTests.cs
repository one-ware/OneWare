using System.Collections.Generic;
using OneWare.Chat.ViewModels;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class ChatStateMigrationTests
{
    private static readonly string[] Services = ["OneWare Cloud", "GitHub Copilot"];

    private static string? Latest(string service) => service == "GitHub Copilot" ? "latest-copilot" : null;

    [Fact]
    public void ResolveLastChat_WithoutState_OpensDefaultServiceWithItsLatestChat()
    {
        var chat = ChatViewModel.ResolveLastChat(null, ["GitHub Copilot"], Latest);

        Assert.Equal(("GitHub Copilot", "latest-copilot"), chat);
    }

    [Fact]
    public void ResolveLastChat_Version1_OpensSelectedServiceWithRememberedSession()
    {
        var state = new ChatViewModel.ChatState
        {
            Version = 1,
            SelectedChatServiceName = "GitHub Copilot",
            SelectedSessionByService = new Dictionary<string, string> { ["GitHub Copilot"] = "remembered" }
        };

        Assert.Equal(("GitHub Copilot", "remembered"), ChatViewModel.ResolveLastChat(state, Services, Latest));
    }

    [Fact]
    public void ResolveLastChat_Version0_SwitchesToDefaultService()
    {
        var state = new ChatViewModel.ChatState { Version = 0, SelectedChatServiceName = "GitHub Copilot" };

        Assert.Equal(("OneWare Cloud", null), ChatViewModel.ResolveLastChat(state, Services, Latest));
    }

    [Fact]
    public void ResolveLastChat_Version2_RestoresLastChat()
    {
        var state = new ChatViewModel.ChatState
        {
            Version = 2,
            LastServiceName = "GitHub Copilot",
            LastSessionId = "last"
        };

        Assert.Equal(("GitHub Copilot", "last"), ChatViewModel.ResolveLastChat(state, Services, Latest));
    }

    [Fact]
    public void ResolveLastChat_Version2_NewChatIsRestoredWithoutSession()
    {
        var state = new ChatViewModel.ChatState { Version = 2, LastServiceName = "GitHub Copilot", LastSessionId = " " };

        Assert.Equal(("GitHub Copilot", null), ChatViewModel.ResolveLastChat(state, Services, Latest));
    }

    [Fact]
    public void ResolveLastChat_Version2_WithUnknownService_FallsBackToDefault()
    {
        var state = new ChatViewModel.ChatState { Version = 2, LastServiceName = "Removed Plugin", LastSessionId = "a" };

        Assert.Equal(("OneWare Cloud", null), ChatViewModel.ResolveLastChat(state, Services, Latest));
    }

    [Fact]
    public void ResolveLastChat_WithoutServices_ReturnsNothing()
    {
        Assert.Null(ChatViewModel.ResolveLastChat(null, [], Latest));
    }
}
