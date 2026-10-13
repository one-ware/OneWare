using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels.Inspector;

// Right panel: combines Variables and Breakpoints in a single dockable with two tabs.
// Same level as AI Chat or Docker Container Manager
public class InspectorViewModel : ExtendedTool
{
    public const string IconKey = "Variable";

    public InspectorViewModel(VariablesViewModel variables,
        BreakpointsViewModel breakpoints) : base(IconKey)
    {
        Variables = variables;
        Breakpoints = breakpoints;

        Id = "DebuggerInspector";
        Title = "Debugger";
    }

    public VariablesViewModel Variables { get; }

    public BreakpointsViewModel Breakpoints { get; }
}
