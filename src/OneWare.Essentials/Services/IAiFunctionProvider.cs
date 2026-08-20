using Microsoft.Extensions.AI;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IAiFunctionProvider
{
    /// <summary>Fired when an AI function starts.</summary>
    event EventHandler<AiFunctionStartedEvent>? FunctionStarted;
    /// <summary>Fired when an AI function completes.</summary>
    event EventHandler<AiFunctionCompletedEvent>? FunctionCompleted;
    /// <summary>Fired when a running AI function reports incremental output.</summary>
    event EventHandler<AiFunctionProgressEvent>? FunctionProgress
    {
        add { }
        remove { }
    }

    /// <summary>Returns available AI tools for this provider.</summary>
    ICollection<AIFunction> GetTools();

    /// <summary>Cancels all AI functions that are currently running.</summary>
    void CancelActiveFunctions()
    {
    }

    /// <summary>Cancels the single running AI function with the given invocation id.</summary>
    void CancelFunction(string id)
    {
    }

    /// <summary>Registers an additional AI function (e.g. from plugins).</summary>
    void RegisterFunction(IOneWareAiFunction function);

    /// <summary>Registers an additional system prompt segment.</summary>
    void RegisterPromptAddition(string promptAddition);

    /// <summary>Returns all registered prompt additions.</summary>
    IReadOnlyCollection<string> GetPromptAdditions();

    /// <summary>
    /// Registers a custom agent the main chat agent can delegate to. Registering an agent whose
    /// <see cref="OneWareAiAgent.Name"/> already exists replaces the previous registration.
    /// </summary>
    /// <remarks>
    /// Agents are applied when a chat session is created, so register them during module
    /// initialization.
    /// </remarks>
    void RegisterAgent(OneWareAiAgent agent)
    {
    }

    /// <summary>Returns all registered custom agents.</summary>
    IReadOnlyCollection<OneWareAiAgent> GetAgents() => [];

    /// <summary>
    /// Registers a skill the AI can load on demand. Registering a skill whose
    /// <see cref="OneWareAiSkill.Name"/> already exists replaces the previous registration.
    /// </summary>
    void RegisterSkill(OneWareAiSkill skill)
    {
    }

    /// <summary>Returns all registered skills.</summary>
    IReadOnlyCollection<OneWareAiSkill> GetSkills() => [];

    /// <summary>
    /// Registers a skill discovery root: a directory whose sub-directories each contain a
    /// <c>SKILL.md</c>. Use this for skills that ship scripts or other resources with the plugin.
    /// </summary>
    void RegisterSkillDirectory(string directory)
    {
    }

    /// <summary>
    /// Returns the skill discovery roots that chat services must pass to the AI backend. Skills
    /// registered with <see cref="RegisterSkill"/> are written to disk on the first call.
    /// </summary>
    IReadOnlyCollection<string> GetSkillDirectories() => [];

    /// <summary>
    /// Returns the <see cref="IOneWareAiFunction.ConfirmationCheck"/> delegate for the named function,
    /// or <see langword="null"/> if the function has no check or is not registered.
    /// </summary>
    Func<AIFunctionArguments, string?>? GetConfirmationCheck(string functionName);
}
