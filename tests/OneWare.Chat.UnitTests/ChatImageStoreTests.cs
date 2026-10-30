using System;
using System.IO;
using OneWare.Chat.Services;
using OneWare.Chat.ViewModels;
using OneWare.Chat.ViewModels.ChatMessages;
using Xunit;

namespace OneWare.Chat.UnitTests;

public sealed class ChatImageStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oneware-chat-images-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void Store_WritesEachImageOncePerSession()
    {
        var store = new ChatImageStore(_root);
        byte[] data = [1, 2, 3];

        var first = store.Store("session-1", data, "image/png");
        var second = store.Store("session-1", data, "image/png");
        var other = store.Store("session-2", data, "image/jpeg");

        Assert.Equal(first, second);
        Assert.Equal(".png", Path.GetExtension(first));
        Assert.Equal(".jpg", Path.GetExtension(other));
        Assert.Equal(data, File.ReadAllBytes(first));
        Assert.Single(Directory.GetFiles(store.GetSessionDirectory("session-1")));
    }

    [Fact]
    public void DeleteSession_RemovesOnlyThatSession()
    {
        var store = new ChatImageStore(_root);
        var kept = store.Store("keep", [1], "image/png");
        var removed = store.Store("remove", [1], "image/png");

        store.DeleteSession("remove");

        Assert.True(File.Exists(kept));
        Assert.False(File.Exists(removed));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..")]
    [InlineData("a/b\\c")]
    public void GetSessionDirectory_StaysInsideTheRoot(string sessionId)
    {
        var directory = new ChatImageStore(_root).GetSessionDirectory(sessionId);

        Assert.Equal(Path.GetFullPath(_root), Path.GetDirectoryName(Path.GetFullPath(directory)));
    }

    [Fact]
    public void Transcript_KeepsImageMessages()
    {
        var image = new ChatMessageImageViewModel("/tmp/image.png", "image/png")
        {
            Description = "Screenshot",
            ToolName = "browser_take_screenshot"
        };

        var state = ChatTranscript.BuildMessageState(image);
        Assert.NotNull(state);
        Assert.True(ChatTranscript.TryCreateMessage(state, out var restored));

        var restoredImage = Assert.IsType<ChatMessageImageViewModel>(restored);
        Assert.Equal(image.FilePath, restoredImage.FilePath);
        Assert.Equal(image.MimeType, restoredImage.MimeType);
        Assert.Equal("Screenshot", restoredImage.Caption);
        Assert.Equal(image.ToolName, restoredImage.ToolName);
    }
}
