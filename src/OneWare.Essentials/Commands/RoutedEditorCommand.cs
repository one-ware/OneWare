using System.Text.RegularExpressions;
using Avalonia.LogicalTree;
using AvaloniaEdit.Editing;
using RoutedCommand = AvaloniaEdit.RoutedCommand;

namespace OneWare.Essentials.Commands;

/// <summary>
///     Application command that wraps an <see cref="AvaloniaEdit.RoutedCommand" /> so the
///     gestures originally registered by AvaloniaEdit's default input handlers can be re-bound
///     through the application command system.
/// </summary>
public class RoutedEditorCommand : ApplicationCommandBase
{
    private readonly RoutedCommand _routedCommand;

    public RoutedEditorCommand(RoutedCommand routedCommand)
        : base(BuildName(routedCommand.Name))
    {
        _routedCommand = routedCommand;
    }

    public override bool Execute(ILogical source)
    {
        var textArea = ResolveTextArea(source);
        if (textArea is null) return false;
        if (!_routedCommand.CanExecute(null, textArea)) return false;
        _routedCommand.Execute(null, textArea);
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        var textArea = ResolveTextArea(source);
        return textArea is not null && _routedCommand.CanExecute(null, textArea);
    }

    private static TextArea? ResolveTextArea(ILogical source)
    {
        return source as TextArea ?? source.FindLogicalAncestorOfType<TextArea>();
    }

    private static readonly Regex SplitCamelCaseRegex =
        new("(?<!^)([A-Z])", RegexOptions.Compiled);

    private static string BuildName(string commandName)
    {
        // "MoveLeftByCharacter" -> "Editor: Move Left By Character"
        var pretty = SplitCamelCaseRegex.Replace(commandName, " $1");
        return $"Editor: {pretty}";
    }
}
