using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels.Inspector;

// Rechtes Panel: fasst Variables und Breakpoints in einem einzigen Dockable mit zwei Reitern
// zusammen. Selbe Ebene wie AI Chat oder Docker Container Manager
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
