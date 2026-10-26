using OneWare.Debugger.Helpers;
using OneWare.Debugger.ViewModels;
using OneWare.Debugger.Views;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger;

// While a session is halted, hovering an identifier in the editor shows its current value instead of
// the language server hover: name, type and an expandable tree for structures and arrays.
public class DebuggerHoverProvider(IDebuggerService debuggerService) : IEditorHoverProvider
{
    public async Task<object?> GetHoverContentAsync(EditorHoverRequest request)
    {
        if (!debuggerService.IsActive) return null;

        var state = debuggerService.State;
        if (state.IsRunning || state.CurrentFrame is not { } frame) return null;

        var document = request.Editor.CurrentDocument;
        if (request.Offset < 0 || request.Offset > document.TextLength) return null;

        var line = document.GetLineByOffset(request.Offset);
        var expression = HoverExpression.Extract(document.GetText(line), request.Offset - line.Offset);
        if (expression == null) return null;

        var root = HoverExpression.RootOf(expression);
        var local = state.Locals.FirstOrDefault(x => x.Name == root);

        // A local belongs to the halted frame. The same name in another file means something else.
        if (local != null && (frame.File is not { } frameFile || request.Editor.FullPath is not { } path ||
                              !DebuggerService.IsSameFile(frameFile, path)))
            return null;

        var isLocal = local != null && expression == local.Name;

        // Locals are listed with simple values only, so structures and arrays come without one.
        var value = isLocal && !string.IsNullOrEmpty(local!.Value)
            ? local.Value
            : await debuggerService.EvaluateExpressionAsync(expression);

        if (string.IsNullOrEmpty(value)) return null;

        // The target may have resumed while the value was read.
        if (debuggerService.State.IsRunning) return null;

        var node = GdbValueParser.Parse(expression, value);
        node.TypeName = isLocal ? local!.TypeName : null;
        node.IsExpanded = node.Children.Count is > 0 and <= 20;

        return new DebuggerHoverView { DataContext = new DebuggerHoverViewModel(node) };
    }
}
