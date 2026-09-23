using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Chat.Services;

/// <summary>
/// Provides the selectable chat agents: the built-in <c>Agent</c>, <c>Plan</c> and <c>Ask</c> modes,
/// agents registered by modules, and markdown agents discovered in <c>.github/agents</c> of the
/// active project and in the user agent directory.
/// </summary>
public class ChatAgentService : ObservableObject, IChatAgentService
{
    /// <summary>Directory name searched for markdown agents inside a project.</summary>
    public const string ProjectAgentDirectory = ".github/agents";

    private const string SelectedAgentSettingKey = "AiChat_SelectedAgent";

    private readonly IPaths _paths;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    private readonly List<ChatAgentDefinition> _builtInAgents;
    private readonly List<ChatAgentDefinition> _registeredAgents = [];
    private readonly List<ChatAgentDefinition> _fileAgents = [];

    private ChatAgentDefinition? _selectedAgent;

    /// <summary>
    /// Id the user last picked (or the persisted one). Kept even while no matching agent exists, so
    /// a project agent that is only discovered after startup still becomes the selection.
    /// </summary>
    private string? _desiredAgentId;

    public ChatAgentService(IPaths paths, IProjectExplorerService projectExplorerService,
        ISettingsService settingsService, ILogger logger)
    {
        _paths = paths;
        _projectExplorerService = projectExplorerService;
        _settingsService = settingsService;
        _logger = logger;

        _builtInAgents = CreateBuiltInAgents();

        if (!settingsService.HasSetting(SelectedAgentSettingKey))
            settingsService.Register(SelectedAgentSettingKey, _builtInAgents[0].Id);

        _desiredAgentId = settingsService.GetSettingValue<string>(SelectedAgentSettingKey);

        if (projectExplorerService is INotifyPropertyChanged notify)
            notify.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IProjectExplorerService.ActiveProject)) Refresh();
            };

        Refresh();
    }

    public ObservableCollection<ChatAgentDefinition> Agents { get; } = [];

    public ChatAgentDefinition? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            // The selector rebuilds its items on every refresh, so ignore the transient null the
            // bound ListBox pushes back while its item source is being replaced.
            if (value == null && Agents.Count > 0) return;
            if (!SetProperty(ref _selectedAgent, value)) return;
            if (value == null) return;

            _desiredAgentId = value.Id;
            _settingsService.SetSettingValue(SelectedAgentSettingKey, value.Id);
        }
    }

    public void RegisterAgent(ChatAgentDefinition agent)
    {
        if (string.IsNullOrWhiteSpace(agent.Id)) throw new ArgumentException("Agent id must not be empty.");

        _registeredAgents.RemoveAll(x => IdEquals(x.Id, agent.Id));
        _registeredAgents.Add(agent);
        RebuildAgents();
    }

    public bool RemoveAgent(string id)
    {
        if (_registeredAgents.RemoveAll(x => IdEquals(x.Id, id)) == 0) return false;

        RebuildAgents();
        return true;
    }

    public bool SelectAgent(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        var match = Agents.FirstOrDefault(x => IdEquals(x.Id, id));
        if (match == null) return false;

        SelectedAgent = match;
        return true;
    }

    public void Refresh()
    {
        _fileAgents.Clear();

        foreach (var directory in GetAgentDirectories())
            LoadAgentsFrom(directory);

        RebuildAgents();
    }

    private IEnumerable<string> GetAgentDirectories()
    {
        yield return Path.Combine(_paths.AppDataDirectory, "Agents");

        var projectRoot = _projectExplorerService.ActiveProject?.FullPath;
        if (!string.IsNullOrEmpty(projectRoot))
            yield return Path.Combine(projectRoot, ".github", "agents");
    }

    private void LoadAgentsFrom(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return;

            foreach (var file in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var agent = ParseAgentFile(file);
                    if (agent == null) continue;

                    _fileAgents.RemoveAll(x => IdEquals(x.Id, agent.Id));
                    _fileAgents.Add(agent);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load chat agent from {File}.", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan chat agent directory {Directory}.", directory);
        }
    }

    /// <summary>
    /// Reads a markdown agent file: an optional YAML front matter block with the agent metadata,
    /// followed by the markdown body used as the agent instructions.
    /// </summary>
    internal static ChatAgentDefinition? ParseAgentFile(string file)
    {
        var text = File.ReadAllText(file);
        var (frontMatter, body) = SplitFrontMatter(text);

        var id = FirstValue(frontMatter, "name", "id") ?? Path.GetFileNameWithoutExtension(file);
        if (string.IsNullOrWhiteSpace(id)) return null;

        var instructions = body.Trim();
        if (instructions.Length == 0) return null;

        var displayName = FirstValue(frontMatter, "displayName", "display_name", "title")
                          ?? ToDisplayName(id);

        return new ChatAgentDefinition
        {
            Id = id.Trim(),
            DisplayName = displayName,
            Description = FirstValue(frontMatter, "description"),
            Instructions = instructions,
            TurnMode = string.Equals(FirstValue(frontMatter, "mode"), "plan", StringComparison.OrdinalIgnoreCase)
                ? ChatAgentTurnMode.Plan
                : ChatAgentTurnMode.Interactive,
            IsReadOnly = ParseBool(FirstValue(frontMatter, "readOnly", "read_only")) ?? false,
            Tools = ParseList(FirstValue(frontMatter, "tools")),
            Model = FirstValue(frontMatter, "model"),
            ReasoningEffort = FirstValue(frontMatter, "reasoningEffort", "reasoning_effort"),
            SourcePath = file
        };
    }

    private static (Dictionary<string, string> FrontMatter, string Body) SplitFrontMatter(string text)
    {
        var frontMatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = text.Replace("\r\n", "\n");

        if (!normalized.StartsWith("---\n", StringComparison.Ordinal)) return (frontMatter, normalized);

        var end = normalized.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0) return (frontMatter, normalized);

        var block = normalized[4..end];

        // Skip the rest of the closing fence line; everything after it is body, including a leading
        // markdown list or horizontal rule.
        var bodyStart = normalized.IndexOf('\n', end + 1);
        var body = bodyStart < 0 ? string.Empty : normalized[(bodyStart + 1)..];

        string? listKey = null;
        var listValues = new List<string>();

        foreach (var rawLine in block.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0 || line.TrimStart().StartsWith('#')) continue;

            // Continuation of a "key:" block that lists its values as "- value" lines.
            if (listKey != null && line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            {
                listValues.Add(Unquote(line.TrimStart()[2..].Trim()));
                frontMatter[listKey] = string.Join(", ", listValues);
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            var value = Unquote(line[(separator + 1)..].Trim());

            listKey = value.Length == 0 ? key : null;
            listValues.Clear();
            frontMatter[key] = value;
        }

        return (frontMatter, body);
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];
        return value;
    }

    private static string? FirstValue(Dictionary<string, string> frontMatter, params string[] keys)
    {
        foreach (var key in keys)
            if (frontMatter.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        return null;
    }

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() is "true" or "yes" or "1";
    }

    private static IReadOnlyList<string>? ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var items = value.Trim().Trim('[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Unquote)
            .Where(x => x.Length > 0)
            .ToArray();

        return items.Length == 0 ? null : items;
    }

    private static string ToDisplayName(string id)
    {
        var parts = id.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private void RebuildAgents()
    {
        var all = _builtInAgents
            .Concat(_registeredAgents.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
            .Concat(_fileAgents.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Agents.Clear();
        foreach (var agent in all) Agents.Add(agent);

        _selectedAgent = Agents.FirstOrDefault(x => IdEquals(x.Id, _desiredAgentId ?? _selectedAgent?.Id))
                         ?? Agents.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedAgent));
    }

    private static bool IdEquals(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static List<ChatAgentDefinition> CreateBuiltInAgents() =>
    [
        new()
        {
            Id = BuiltInChatAgents.Agent,
            DisplayName = "Agent",
            Description = "Full access: researches, edits files and runs tools to complete the task.",
            IsBuiltIn = true
        },
        new()
        {
            Id = BuiltInChatAgents.Plan,
            DisplayName = "Plan",
            Description = "Researches the codebase and works out a plan before anything is changed.",
            TurnMode = ChatAgentTurnMode.Plan,
            IsReadOnly = true,
            IsBuiltIn = true,
            Instructions = """
                           You are in plan mode. Investigate the codebase and produce an implementation
                           plan for the user's request instead of carrying it out.

                           - Do not modify files, run commands with side effects, or change IDE state.
                           - Read the code you need first; never plan against assumptions.
                           - Deliver a concise, ordered plan of concrete steps with the files involved,
                             and call out open questions, risks and decisions the user has to make.
                           - Finish by presenting the plan with the exit_plan_mode tool, so the user can
                             start the implementation or have the plan changed. Never implement before
                             the user accepted it.
                           """
        },
        new()
        {
            Id = BuiltInChatAgents.Ask,
            DisplayName = "Ask",
            Description = "Answers questions about the code base without changing anything.",
            IsReadOnly = true,
            IsBuiltIn = true,
            Instructions = """
                           You are in ask mode: answer the user's question, do not carry out work.

                           - Do not modify files, run commands with side effects, or change IDE state.
                           - Read the relevant code before answering; never answer from assumptions.
                           - Answer directly and concisely, with short code examples where they help.
                           """
        }
    ];
}
