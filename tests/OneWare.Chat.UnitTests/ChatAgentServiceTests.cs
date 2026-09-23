using System;
using System.IO;
using OneWare.Chat.Services;
using OneWare.Essentials.Models;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class ChatAgentServiceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "OneWareAgentTests", Guid.NewGuid().ToString("N"));

    public ChatAgentServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string WriteAgent(string fileName, string content)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ParseAgentFile_ReadsFrontMatterAndBody()
    {
        var path = WriteAgent("review.md", """
                                           ---
                                           name: code-review
                                           displayName: Code Review
                                           description: Reviews a diff.
                                           mode: plan
                                           readOnly: true
                                           model: claude-opus-5
                                           reasoningEffort: high
                                           tools: [readFile, getAllErrors]
                                           ---

                                           Review the change and report bugs.
                                           """);

        var agent = ChatAgentService.ParseAgentFile(path);

        Assert.NotNull(agent);
        Assert.Equal("code-review", agent!.Id);
        Assert.Equal("Code Review", agent.DisplayName);
        Assert.Equal("Reviews a diff.", agent.Description);
        Assert.Equal(ChatAgentTurnMode.Plan, agent.TurnMode);
        Assert.True(agent.IsReadOnly);
        Assert.Equal("claude-opus-5", agent.Model);
        Assert.Equal("high", agent.ReasoningEffort);
        Assert.Equal(["readFile", "getAllErrors"], agent.Tools);
        Assert.Equal("Review the change and report bugs.", agent.Instructions);
        Assert.Equal(path, agent.SourcePath);
        Assert.False(agent.IsBuiltIn);
    }

    [Fact]
    public void ParseAgentFile_ReadsToolsFromYamlList()
    {
        var path = WriteAgent("tools.md", """
                                          ---
                                          name: docs
                                          tools:
                                            - readFile
                                            - "getOpenFiles"
                                          ---

                                          Write documentation.
                                          """);

        var agent = ChatAgentService.ParseAgentFile(path);

        Assert.NotNull(agent);
        Assert.Equal(["readFile", "getOpenFiles"], agent!.Tools);
    }

    [Fact]
    public void ParseAgentFile_FallsBackToFileNameAndDefaults()
    {
        var path = WriteAgent("release-notes.md", "Summarize the release.");

        var agent = ChatAgentService.ParseAgentFile(path);

        Assert.NotNull(agent);
        Assert.Equal("release-notes", agent!.Id);
        Assert.Equal("Release Notes", agent.DisplayName);
        Assert.Equal(ChatAgentTurnMode.Interactive, agent.TurnMode);
        Assert.False(agent.IsReadOnly);
        Assert.Null(agent.Tools);
        Assert.Equal("Summarize the release.", agent.Instructions);
    }

    [Fact]
    public void ParseAgentFile_KeepsBodyStartingWithAList()
    {
        var path = WriteAgent("reviewer.md", """
                                             ---
                                             name: reviewer
                                             ---

                                             - Review the diff
                                             - Report bugs
                                             """);

        var agent = ChatAgentService.ParseAgentFile(path);

        Assert.NotNull(agent);
        Assert.Equal("- Review the diff\n- Report bugs", agent!.Instructions);
    }

    [Fact]
    public void ParseAgentFile_IgnoresFilesWithoutInstructions()
    {
        var path = WriteAgent("empty.md", """
                                          ---
                                          name: empty
                                          ---
                                          """);

        Assert.Null(ChatAgentService.ParseAgentFile(path));
    }
}
