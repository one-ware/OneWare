using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Debugger.ViewModels.Inspector;

namespace OneWare.Debugger.ViewModels.Main;

// The tab area of the debugger panel. Holds all tabs and is the only one that knows
// their order -> outside nobody has to handle an index.
public partial class MainPanelViewModel : ObservableObject
{
    private const int ConsoleTabIndex = 4;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowsValueFormat))]
    private int _selectedTabIndex;

    public MainPanelViewModel(VariablesViewModel variables, RegisterTabViewModel registers,
        MemoryTabViewModel memory, BreakpointsViewModel breakpoints, ConsoleTabViewModel console)
    {
        Variables = variables;
        Registers = registers;
        Memory = memory;
        Breakpoints = breakpoints;
        Console = console;
    }

    // Variables, Registers and Memory share one number base (see ValueFormatViewModel).
    public ValueFormatViewModel ValueFormat => Registers.ValueFormat;

    // The Base selector only makes sense on the tabs that show values: Variables (0), Registers (1), Memory (2).
    public bool ShowsValueFormat => SelectedTabIndex is >= 0 and <= 2;

    public VariablesViewModel Variables { get; }

    public RegisterTabViewModel Registers { get; }

    public MemoryTabViewModel Memory { get; }

    public BreakpointsViewModel Breakpoints { get; }

    public ConsoleTabViewModel Console { get; }

    // On start, the traffic with the backend is what matters -> clear the console and bring it to the front.
    public void ShowConsole()
    {
        Console.Clear();
        SelectedTabIndex = ConsoleTabIndex;
    }
}
