using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Rechtes Panel: fasst Variables, Expressions und Breakpoints in einem einzigen Dockable
///     mit drei Reitern zusammen.
///     Bewusst nur ein Dockable statt dreier: Das Standardlayout legt fuer angepinnte Werkzeuge
///     denselben Bereich an wie fuer den AI-Chat, und diese Konfiguration mit genau einem
///     Dockable ist die einzige, die stabil laeuft. Mehrere angepinnte Dockables laufen haeufiger
///     in den fehlerhaften Schliessen-Pfad der Dock-Bibliothek.
/// </summary>
public class DebuggerInspectorViewModel : ExtendedTool
{
    public const string IconKey = "Variable";

    public DebuggerInspectorViewModel(DebuggerVariablesViewModel variables,
        DebuggerExpressionsViewModel expressions, DebuggerBreakpointsViewModel breakpoints) : base(IconKey)
    {
        Variables = variables;
        Expressions = expressions;
        Breakpoints = breakpoints;

        Id = "DebuggerInspector";
        Title = "Debugging";
    }

    public DebuggerVariablesViewModel Variables { get; }

    public DebuggerExpressionsViewModel Expressions { get; }

    public DebuggerBreakpointsViewModel Breakpoints { get; }
}
