using System.Collections.ObjectModel;
using System.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

/// <summary>
/// Keeps the list of selectable chat agents and the agent the user currently chats with.
/// </summary>
/// <remarks>
/// Chat services read <see cref="SelectedAgent"/> when they send a message and apply its turn mode,
/// instructions and tool restrictions to that turn.
/// </remarks>
public interface IChatAgentService : INotifyPropertyChanged
{
    /// <summary>
    /// Built-in, module-registered and file-based agents, in display order.
    /// </summary>
    ObservableCollection<ChatAgentDefinition> Agents { get; }

    /// <summary>
    /// The agent used for new messages. Never <see langword="null"/> while agents exist.
    /// </summary>
    ChatAgentDefinition? SelectedAgent { get; set; }

    /// <summary>
    /// Adds an agent or replaces the one with the same <see cref="ChatAgentDefinition.Id"/>.
    /// </summary>
    void RegisterAgent(ChatAgentDefinition agent);

    /// <summary>
    /// Removes a registered agent. Built-in agents cannot be removed.
    /// </summary>
    bool RemoveAgent(string id);

    /// <summary>
    /// Re-reads the file-based agents of the active project.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Selects the agent with the given id, if it exists.
    /// </summary>
    bool SelectAgent(string? id);
}
