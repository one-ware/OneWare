using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.ViewModels.Main;

// Der Reiterbereich des unteren Panels. Haelt die drei Reiter und weiss als Einziger, in
// welcher Reihenfolge sie stehen -> ausserhalb muss niemand mit einem Index hantieren.
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

    // Beim Start interessiert der Verkehr mit dem Backend -> Konsole leeren und nach vorn.
    public void ShowConsole()
    {
        Console.Clear();
        SelectedTabIndex = ConsoleTabIndex;
    }
}
