namespace OneWare.Chat.ViewModels;

internal enum ChatActivityPhase
{
    Working,
    Thinking,
    Writing
}

/// <summary>
/// Derives the text next to the busy indicator from what the agent is doing right now, e.g.
/// "Thinking…" or "Running Execute In Terminal…". Prompts that wait for the user win
/// over steering, which wins over running tools, running sub-agents and the model's phase.
/// </summary>
internal sealed class ChatActivityStatus
{
    private readonly List<KeyValuePair<object, string>> _pendingPrompts = [];
    private readonly List<KeyValuePair<string, string>> _runningTools = [];
    private readonly List<KeyValuePair<string, string>> _runningSubAgents = [];

    public ChatActivityPhase Phase { get; set; }

    public bool IsSteering { get; set; }

    public void ToolStarted(string id, string name) => Add(_runningTools, id, name);

    public void ToolCompleted(string id)
    {
        // The model continues with the tool result.
        if (Remove(_runningTools, id)) Phase = ChatActivityPhase.Working;
    }

    public void SubAgentStarted(string id, string name) => Add(_runningSubAgents, id, name);

    public void SubAgentCompleted(string id) => Remove(_runningSubAgents, id);

    /// <param name="waitingText">Shown while the prompt is open, e.g. "Waiting for approval…".</param>
    public void PromptOpened(object prompt, string waitingText)
    {
        PromptClosed(prompt);
        _pendingPrompts.Add(new KeyValuePair<object, string>(prompt, waitingText));
    }

    public void PromptClosed(object prompt) => _pendingPrompts.RemoveAll(x => ReferenceEquals(x.Key, prompt));

    public void Reset()
    {
        _pendingPrompts.Clear();
        _runningTools.Clear();
        _runningSubAgents.Clear();
        Phase = ChatActivityPhase.Working;
        IsSteering = false;
    }

    public string GetText()
    {
        if (_pendingPrompts.Count > 0) return _pendingPrompts[^1].Value;
        if (IsSteering) return "Steering…";

        if (_runningTools.Count == 1) return $"Running {_runningTools[0].Value}…";
        if (_runningTools.Count > 1) return $"Running {_runningTools.Count} tools…";

        if (_runningSubAgents.Count == 1) return $"Waiting for {_runningSubAgents[0].Value}…";
        if (_runningSubAgents.Count > 1) return $"Waiting for {_runningSubAgents.Count} subagents…";

        return Phase switch
        {
            ChatActivityPhase.Thinking => "Thinking…",
            ChatActivityPhase.Writing => "Writing…",
            _ => "Working…"
        };
    }

    private static void Add(List<KeyValuePair<string, string>> list, string id, string name)
    {
        Remove(list, id);
        list.Add(new KeyValuePair<string, string>(id, name));
    }

    private static bool Remove(List<KeyValuePair<string, string>> list, string id) =>
        list.RemoveAll(x => string.Equals(x.Key, id, StringComparison.Ordinal)) > 0;
}
