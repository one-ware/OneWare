using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.ViewModels.Main;

// The tab area of the bottom panel. Holds the three tabs and is the only one that knows
// their order -> outside nobody has to handle an index.
public partial class MainPanelViewModel : ObservableObject
{
    private const int ConsoleTabIndex = 2;

    [ObservableProperty] private int _selectedTabIndex;

    public MainPanelViewModel(RegisterTabViewModel registers, MemoryTabViewModel memory,
        ConsoleTabViewModel console)
    {
        Registers = registers;
        Memory = memory;
        Console = console;
    }

    public RegisterTabViewModel Registers { get; }

    public MemoryTabViewModel Memory { get; }

    public ConsoleTabViewModel Console { get; }

    // On start, the traffic with the backend is what matters -> clear the console and bring it to the front.
    public void ShowConsole()
    {
        Console.Clear();
        SelectedTabIndex = ConsoleTabIndex;
    }
}
