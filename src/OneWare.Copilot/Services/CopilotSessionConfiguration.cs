using System.IO;
using System.Text;
using GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace OneWare.Copilot.Services;

internal static class CopilotSessionConfiguration
{
    internal static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static readonly string[] ProjectSkillFolders =
        [Path.Combine(".github", "skills"), Path.Combine(".agents", "skills"), Path.Combine(".claude", "skills")];

    private static readonly string[] ProjectInstructionFiles =
        ["AGENTS.md", Path.Combine(".github", "copilot-instructions.md")];

    internal const int MaxInstructionFileChars = 32_000;

    /// <summary>
    /// Project skill roots of the open projects. The runtime only discovers those of the session's working
    /// directory itself, so that project is skipped to avoid loading its skills twice.
    /// </summary>
    internal static IEnumerable<string> GetProjectSkillDirectories(IEnumerable<string> projectRoots,
        string workingDirectory)
    {
        return projectRoots
            .Where(root => !PathComparer.Equals(Path.GetFullPath(root), Path.GetFullPath(workingDirectory)))
            .SelectMany(root => ProjectSkillFolders.Select(folder => Path.Combine(root, folder)))
            .Where(Directory.Exists);
    }

    /// <summary>
    /// Describes the open projects and inlines the instruction files of those the runtime does not load itself
    /// (every project except the working directory). Returns <see langword="null"/> when there is nothing beyond
    /// what the runtime already knows. <paramref name="key"/> changes whenever the context would change.
    /// </summary>
    internal static string? BuildWorkspaceContext(IReadOnlyList<string> projectRoots, string workingDirectory,
        out string key)
    {
        var workingRoot = Path.GetFullPath(workingDirectory);
        var keyBuilder = new StringBuilder(workingRoot);
        var others = projectRoots.Where(x => !PathComparer.Equals(Path.GetFullPath(x), workingRoot)).ToList();

        if (others.Count == 0)
        {
            key = keyBuilder.ToString();
            return null;
        }

        var text = new StringBuilder();
        text.AppendLine("<open-projects>");
        text.AppendLine($"Your working directory is {workingRoot}. The IDE also has these projects open:");
        foreach (var root in projectRoots)
        {
            var marker = PathComparer.Equals(Path.GetFullPath(root), workingRoot) ? " (working directory)" : "";
            text.AppendLine($"- {Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}: {root}{marker}");
        }

        text.AppendLine("Follow each project's instructions below when working in it, and read more specific " +
                        "AGENTS.md files in its subdirectories before working there.");

        foreach (var root in others)
        {
            keyBuilder.Append('|').Append(root);
            foreach (var file in ProjectInstructionFiles.Select(x => Path.Combine(root, x)))
            {
                if (!File.Exists(file)) continue;

                string content;
                try
                {
                    var info = new FileInfo(file);
                    keyBuilder.Append('|').Append(file).Append(':').Append(info.Length).Append(':')
                        .Append(info.LastWriteTimeUtc.Ticks);
                    content = File.ReadAllText(file).Trim();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (content.Length == 0) continue;
                if (content.Length > MaxInstructionFileChars)
                    content = content[..MaxInstructionFileChars] +
                              $"\n[truncated, read {file} for the rest]";

                text.AppendLine($"<instructions path=\"{file}\">");
                text.AppendLine(content);
                text.AppendLine("</instructions>");
            }
        }

        text.Append("</open-projects>");
        key = keyBuilder.ToString();
        return text.ToString();
    }
    internal static string GetConfigDirectory()
    {
        var copilotHome = Environment.GetEnvironmentVariable("COPILOT_HOME");
        return Path.GetFullPath(string.IsNullOrWhiteSpace(copilotHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot")
            : copilotHome);
    }

    internal static void ApplyDiscovery(SessionConfigBase config, string workingDirectory,
        IList<string> skillDirectories)
    {
        config.WorkingDirectory = workingDirectory;
        // BYOK runtimes have separate state directories, but use the same user customizations.
        config.ConfigDirectory = GetConfigDirectory();
        config.EnableConfigDiscovery = true;
        config.EnableOnDemandInstructionDiscovery = true;
        config.EnableSkills = true;
        config.SkillDirectories = skillDirectories;
        // Discovering user customizations must not opt into executing repository hooks.
        config.EnableFileHooks = false;
        // Keep MCP sign-ins (connectors) in the OS keychain instead of asking again in every chat.
        config.McpOAuthTokenStorage = McpOAuthTokenStorageMode.Persistent;
        // Connectors can add hundreds of tools (Microsoft 365 alone ~400), more than fits into the model's context.
        // Tool search keeps them out of the prompt until the model looks them up.
        config.ToolSearch = new ToolSearchConfig { Enabled = true };
    }

    internal static IList<string> BuildSkillDirectories(IEnumerable<string> pluginDirectories,
        IEnumerable<string> userDirectories, string workingDirectory, Action<string> warn)
    {
        var comparer = PathComparer;
        var directories = new HashSet<string>(comparer);
        foreach (var entry in pluginDirectories.Concat(userDirectories))
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;

            var directory = ResolveUserPath(entry, workingDirectory);
            if (Directory.Exists(directory))
                directories.Add(directory);
            else
                warn($"Skill directory does not exist: {directory}");
        }

        return directories.ToList();
    }

    /// <summary>Resolves a path from the settings: absolute, <c>~/</c> or relative to the working directory.</summary>
    internal static string ResolveUserPath(string entry, string workingDirectory)
    {
        var path = entry.Trim();
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) ||
            path.StartsWith(@"~\", StringComparison.Ordinal))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path.Length == 1 ? "" : path[2..]);

        return Path.GetFullPath(path, workingDirectory);
    }

    /// <summary>
    /// Marks the functions flagged <see cref="OneWare.Essentials.Models.IOneWareAiFunction.AlwaysLoaded" /> so tool
    /// search never defers them. All other tools (plugins, MCP servers) may be deferred behind tool search.
    /// </summary>
    internal static List<AIFunctionDeclaration> ApplyToolDeferral(IEnumerable<AIFunction> tools,
        Func<string, bool> isAlwaysLoaded) =>
        tools.Select(AIFunctionDeclaration (x) => isAlwaysLoaded(x.Name) ? new AlwaysLoadedTool(x) : x).ToList();

    // Same as CopilotTool.DefineTool with CopilotToolOptions.Defer = Never, for an existing function.
    private sealed class AlwaysLoadedTool(AIFunction inner) : DelegatingAIFunction(inner)
    {
        private const string DeferKey = "defer";

        public override IReadOnlyDictionary<string, object?> AdditionalProperties { get; } =
            new Dictionary<string, object?>(inner.AdditionalProperties) { [DeferKey] = CopilotToolDefer.Never };
    }

    internal static ToolSet BuildAvailableTools() => new ToolSet()
        .AddCustom("*")
        .AddMcp("*")
        .AddBuiltIn(BuiltInTools.Isolated);

    /// <summary>The runtime's tool search, which only looks up other tools.</summary>
    internal const string ToolSearchToolName = "tool_search_tool";

    internal static bool IsExternalTool(string toolName, bool isClientTool) =>
        !isClientTool && toolName != ToolSearchToolName &&
        !BuiltInTools.Isolated.Contains(toolName, StringComparer.Ordinal);
}
