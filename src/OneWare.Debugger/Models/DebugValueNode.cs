using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

// One node of a value as the debugger hover shows it: a scalar, or a structure / array whose
// members are the children.
public partial class DebugValueNode : ObservableObject
{
    [ObservableProperty] private bool _isExpanded;

    public DebugValueNode(string name, string value, IReadOnlyList<DebugValueNode>? children = null)
    {
        Name = name;
        Value = value;
        Children = children ?? [];
    }

    public string Name { get; }

    // Scalar text, or for composites a one-line summary of the members.
    public string Value { get; }

    public string? TypeName { get; set; }

    public IReadOnlyList<DebugValueNode> Children { get; }
}
