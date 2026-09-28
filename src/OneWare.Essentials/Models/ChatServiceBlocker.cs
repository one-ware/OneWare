using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace OneWare.Essentials.Models;

/// <summary>
/// Describes why a chat service cannot be used yet (e.g. not logged in or missing subscription).
/// While set, the chat is hidden and replaced by a panel showing this information.
/// </summary>
public sealed class ChatServiceBlocker(string title, string message)
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    /// <summary>
    /// Optional primary action that resolves the blocker (e.g. "Login with GitHub").
    /// The command receives the clicked control, usable as dialog owner.
    /// </summary>
    public string? ActionText { get; init; }

    public IRelayCommand<Control?>? ActionCommand { get; init; }

    /// <summary>
    /// Offers a refresh button that initializes the service again, e.g. after upgrading a subscription in the browser.
    /// </summary>
    public bool CanRefresh { get; init; } = true;
}
