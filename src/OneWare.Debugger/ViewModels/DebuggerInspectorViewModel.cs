using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

// Rechtes Panel: fasst Variables und Breakpoints in einem einzigen Dockable mit zwei Reitern
// zusammen.
// Bewusst nur ein Dockable statt dreier: Das Standardlayout legt fuer angepinnte Werkzeuge
// denselben Bereich an wie fuer den AI-Chat, und diese Konfiguration mit genau einem
// Dockable ist die einzige, die stabil laeuft. Mehrere angepinnte Dockables laufen haeufiger
// in den fehlerhaften Schliessen-Pfad der Dock-Bibliothek.
public class DebuggerInspectorViewModel : ExtendedTool
{
    public const string IconKey = "Variable";

    public DebuggerInspectorViewModel(DebuggerVariablesViewModel variables,
        DebuggerBreakpointsViewModel breakpoints) : base(IconKey)
    {
        Variables = variables;
        Breakpoints = breakpoints;

        Id = "DebuggerInspector";
        Title = "Debugger";
    }

    public DebuggerVariablesViewModel Variables { get; }

    public DebuggerBreakpointsViewModel Breakpoints { get; }
}
