using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using OneWare.Copilot.Services;
using Xunit;

namespace OneWare.Copilot.UnitTests;

public sealed class CopilotSessionConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OneWare-Copilot-" + Guid.NewGuid());

    public CopilotSessionConfigurationTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyDiscovery_UsesTheSameCustomizationSourcesForCreateAndResume(bool resume)
    {
        SessionConfigBase config = resume ? new ResumeSessionConfig() : new SessionConfig();
        IList<string> skills = [Path.Combine(_root, "skills")];

        CopilotSessionConfiguration.ApplyDiscovery(config, _root, skills);

        Assert.Equal(_root, config.WorkingDirectory);
        Assert.Equal(CopilotSessionConfiguration.GetConfigDirectory(), config.ConfigDirectory);
        Assert.True(config.EnableConfigDiscovery);
        Assert.True(config.EnableOnDemandInstructionDiscovery);
        Assert.True(config.EnableSkills);
        Assert.Same(skills, config.SkillDirectories);
        Assert.False(config.EnableFileHooks);
    }

    [Fact]
    public void GetConfigDirectory_UsesCopilotHomeRatherThanProviderState()
    {
        var copilotHome = Environment.GetEnvironmentVariable("COPILOT_HOME");
        var expected = string.IsNullOrWhiteSpace(copilotHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot")
            : copilotHome;

        Assert.Equal(Path.GetFullPath(expected), CopilotSessionConfiguration.GetConfigDirectory());
    }

    [Fact]
    public void BuildSkillDirectories_MergesPluginAndUserRootsAndResolvesProjectRelativePaths()
    {
        var plugin = Directory.CreateDirectory(Path.Combine(_root, "plugin")).FullName;
        var user = Directory.CreateDirectory(Path.Combine(_root, "user skills")).FullName;
        var warnings = new List<string>();

        var directories = CopilotSessionConfiguration.BuildSkillDirectories(
            [plugin], ["user skills", $"  {plugin}  ", "", " "], _root, warnings.Add);

        Assert.Equal([plugin, user], directories);
        Assert.Empty(warnings);
    }

    [Fact]
    public void BuildSkillDirectories_ExpandsUserHomeAndDeduplicatesNormalizedPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var warnings = new List<string>();

        var directories = CopilotSessionConfiguration.BuildSkillDirectories(
            [], ["~", "~/.", home], _root, warnings.Add);

        Assert.Equal([Path.GetFullPath(home)], directories);
        Assert.Empty(warnings);
    }

    [Fact]
    public void BuildSkillDirectories_ReportsMissingRootsWithoutDiscardingValidOnes()
    {
        var missing = Path.Combine(_root, "missing");
        var warnings = new List<string>();

        var directories = CopilotSessionConfiguration.BuildSkillDirectories(
            [_root], ["missing"], _root, warnings.Add);

        Assert.Equal([_root], directories);
        Assert.Equal([$"Skill directory does not exist: {missing}"], warnings);
    }

    [Fact]
    public void BuildAvailableTools_AllowsMcpWithoutEnablingHostBuiltIns()
    {
        var tools = CopilotSessionConfiguration.BuildAvailableTools();

        Assert.Contains("custom:*", tools);
        Assert.Contains("mcp:*", tools);
        foreach (var name in BuiltInTools.Isolated)
            Assert.Contains($"builtin:{name}", tools);
        Assert.DoesNotContain("builtin:*", tools);
        foreach (var name in new[] { "view", "edit", "create", "bash" })
            Assert.DoesNotContain($"builtin:{name}", tools);
    }

    [Theory]
    [InlineData("filesystem-write_file", false, true)]
    [InlineData("github-list_issues", false, true)]
    [InlineData("readFile", true, false)]
    [InlineData("editFile", true, false)]
    [InlineData("task", false, false)]
    [InlineData("skill", false, false)]
    [InlineData("ask_user", false, false)]
    [InlineData("tool_search_tool", false, false)]
    public void IsExternalTool_SeparatesMcpPermissionsFromTrustedIdeAndIsolatedTools(
        string toolName, bool isClientTool, bool expected)
    {
        Assert.Equal(expected, CopilotSessionConfiguration.IsExternalTool(toolName, isClientTool));
    }

    [Fact]
    public async Task ApplyToolDeferral_KeepsOnlyAlwaysLoadedToolsLoaded()
    {
        var core = AIFunctionFactory.Create(() => "content", new AIFunctionFactoryOptions
        {
            Name = "readFile",
            AdditionalProperties = new Dictionary<string, object?> { ["custom"] = 1 }
        });
        var plugin = AIFunctionFactory.Create(() => "x", "pluginTool");

        var tools = CopilotSessionConfiguration.ApplyToolDeferral([core, plugin], name => name == "readFile");

        var wrapped = Assert.IsAssignableFrom<AIFunction>(tools[0]);
        Assert.Equal("readFile", wrapped.Name);
        Assert.Equal(CopilotToolDefer.Never, wrapped.AdditionalProperties["defer"]);
        Assert.Equal(1, wrapped.AdditionalProperties["custom"]);
        Assert.Equal("content", (await wrapped.InvokeAsync()).ToString()?.Trim('"'));

        Assert.Same(plugin, tools[1]);
        Assert.False(plugin.AdditionalProperties.ContainsKey("defer"));
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }
}
