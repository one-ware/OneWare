using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Commands;

/// <summary>
///     Application command that executes an AvaloniaEdit <see cref="RoutedCommand" /> on the focused <see cref="TextArea" />.
/// </summary>
/// <remarks>
///     To let the application command system own the shortcut, create the command with
///     <paramref name="takeOverGesture" /> set to <see langword="true" />. The command then uses the AvaloniaEdit
///     gesture as its default and clears <see cref="RoutedCommand.Gesture" />, so the shortcut can be rebound.
/// </remarks>
public class TextAreaApplicationCommand : ApplicationCommandBase
{
    public TextAreaApplicationCommand(string name, RoutedCommand command, bool takeOverGesture = false) : base(name)
    {
        Command = command;

        if (!takeOverGesture) return;

        DefaultGesture = command.Gesture;
        command.Gesture = null;
    }

    public RoutedCommand Command { get; }

    public override bool Execute(ILogical source)
    {
        var textArea = FindTextArea(source);
        if (textArea == null || !Command.CanExecute(null, textArea)) return false;

        Command.Execute(null, textArea);
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        var textArea = FindTextArea(source);
        return textArea != null && Command.CanExecute(null, textArea);
    }

    private static TextArea? FindTextArea(ILogical source)
    {
        return source switch
        {
            TextArea textArea => textArea,
            TextEditor textEditor => textEditor.TextArea,
            // The command manager executes commands with the main window as source, so use the active editor.
            TopLevel => GetCurrentEditorTextArea(),
            _ => source.FindLogicalAncestorOfType<TextArea>()
                 ?? (source as Visual)?.FindAncestorOfType<TextArea>()
        };
    }

    private static TextArea? GetCurrentEditorTextArea()
    {
        var dockService = ContainerLocator.Container?.GetService<IMainDockService>();
        return (dockService?.CurrentDocument as IEditor)?.Editor.TextArea;
    }
}
