using AvaloniaEdit;
using AvaloniaEdit.Search;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Services;
using EditCommands = AvaloniaEdit.ApplicationCommands;

namespace OneWare.Core.Services;

/// <summary>
///     Registers the AvaloniaEdit editor commands as application commands, so their shortcuts can be changed
///     in the command manager.
/// </summary>
internal static class EditorApplicationCommands
{
    public static void Register(IApplicationCommandService applicationCommandService)
    {
        // Built-in AvaloniaEdit shortcuts: the application command system takes them over.
        Register(applicationCommandService, "Editor: Undo", EditCommands.Undo, true);
        Register(applicationCommandService, "Editor: Redo", EditCommands.Redo, true);
        Register(applicationCommandService, "Editor: Cut", EditCommands.Cut, true);
        Register(applicationCommandService, "Editor: Copy", EditCommands.Copy, true);
        Register(applicationCommandService, "Editor: Paste", EditCommands.Paste, true);
        Register(applicationCommandService, "Editor: Select All", EditCommands.SelectAll, true);
        Register(applicationCommandService, "Editor: Find", EditCommands.Find, true);
        Register(applicationCommandService, "Editor: Replace", EditCommands.Replace, true);
        Register(applicationCommandService, "Editor: Find Next", SearchCommands.FindNext, true);
        Register(applicationCommandService, "Editor: Find Previous", SearchCommands.FindPrevious, true);
        Register(applicationCommandService, "Editor: Delete Line", AvaloniaEditCommands.DeleteLine, true);
        Register(applicationCommandService, "Editor: Indent Selection", AvaloniaEditCommands.IndentSelection, true);
        Register(applicationCommandService, "Editor: Toggle Overstrike", AvaloniaEditCommands.ToggleOverstrike, true);

        // Commands without a default shortcut.
        Register(applicationCommandService, "Editor: Convert to Uppercase", AvaloniaEditCommands.ConvertToUppercase);
        Register(applicationCommandService, "Editor: Convert to Lowercase", AvaloniaEditCommands.ConvertToLowercase);
        Register(applicationCommandService, "Editor: Convert to Title Case", AvaloniaEditCommands.ConvertToTitleCase);
        Register(applicationCommandService, "Editor: Invert Case", AvaloniaEditCommands.InvertCase);
        Register(applicationCommandService, "Editor: Remove Leading Whitespace",
            AvaloniaEditCommands.RemoveLeadingWhitespace);
        Register(applicationCommandService, "Editor: Remove Trailing Whitespace",
            AvaloniaEditCommands.RemoveTrailingWhitespace);
        Register(applicationCommandService, "Editor: Convert Tabs to Spaces", AvaloniaEditCommands.ConvertTabsToSpaces);
        Register(applicationCommandService, "Editor: Convert Spaces to Tabs", AvaloniaEditCommands.ConvertSpacesToTabs);
        Register(applicationCommandService, "Editor: Convert Leading Tabs to Spaces",
            AvaloniaEditCommands.ConvertLeadingTabsToSpaces);
        Register(applicationCommandService, "Editor: Convert Leading Spaces to Tabs",
            AvaloniaEditCommands.ConvertLeadingSpacesToTabs);
    }

    private static void Register(IApplicationCommandService applicationCommandService, string name,
        RoutedCommand command, bool takeOverGesture = false)
    {
        applicationCommandService.RegisterCommand(new TextAreaApplicationCommand(name, command, takeOverGesture));
    }
}
