using System;
using System.Collections.Generic;
using OneWare.Essentials.Enums;

namespace OneWare.Essentials.Models;

public enum MessageBoxButtonRole
{
    Cancel,
    Yes,
    No,
    Custom
}

public enum MessageBoxButtonStyle
{
    Primary,
    Secondary,
    Danger,
}

public enum MessageBoxInputKind
{
    None,
    Text,
    Password
}

public sealed class MessageBoxInputOptions
{
    public MessageBoxInputKind Kind { get; set; } = MessageBoxInputKind.Text;
    public bool IsRequired { get; set; }
    public bool ShowFolderButton { get; set; }
    public string? Label { get; set; }
    public string? Placeholder { get; set; }
    public string? DefaultValue { get; set; }
}

public sealed class MessageBoxButton
{
    public string Text { get; set; } = "";
    public MessageBoxButtonRole Role { get; set; } = MessageBoxButtonRole.Custom;
    public MessageBoxButtonStyle Style { get; set; } = MessageBoxButtonStyle.Secondary;
    public bool IsDefault { get; set; }
    public bool IsCancel => Role == MessageBoxButtonRole.Cancel;
}

public sealed class MessageBoxRequest
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public MessageBoxIcon Icon { get; set; } = MessageBoxIcon.Info;
    public IReadOnlyList<MessageBoxButton> Buttons { get; set; } = Array.Empty<MessageBoxButton>();
    public MessageBoxInputOptions? Input { get; set; }
    public IReadOnlyList<object>? SelectionItems { get; set; }
    public object? SelectedItem { get; set; }
    public bool SelectionRequired { get; set; }
}

public sealed class MessageBoxResult
{
    public MessageBoxButton? Button { get; init; }
    public string? Input { get; init; }
    public object? SelectedItem { get; init; }

    public MessageBoxButtonRole Role => Button?.Role ?? MessageBoxButtonRole.Cancel;

    public bool IsCanceled => Role == MessageBoxButtonRole.Cancel || Button == null;
    public bool IsAccepted => Role is MessageBoxButtonRole.Yes;

    public MessageBoxStatus Status => Role switch
    {
        MessageBoxButtonRole.Yes => MessageBoxStatus.Yes,
        MessageBoxButtonRole.No => MessageBoxStatus.No,
        _ => MessageBoxStatus.Canceled
    };

    public static MessageBoxResult Canceled()
    {
        return new MessageBoxResult { Button = null };
    }
}
