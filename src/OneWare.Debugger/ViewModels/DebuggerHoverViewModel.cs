using OneWare.Debugger.Models;

namespace OneWare.Debugger.ViewModels;

// Content of the editor hover while halted. The hovered value is the single root, so the tree shows
// it as the first row and its members below.
public class DebuggerHoverViewModel(DebugValueNode value)
{
    public IReadOnlyList<DebugValueNode> Roots { get; } = [value];
}
