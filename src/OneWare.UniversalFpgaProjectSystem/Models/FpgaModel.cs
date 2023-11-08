using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaModel : ObservableObject
{
    public string Name { get; }

    public Dictionary<string, FpgaPin> AvailablePins { get; } = new();
    
    private CompileConnectionModel _selectedConnection;
    
    private FpgaPin _selectedPin;
    public FpgaPin SelectedPin
    {
        get => _selectedPin;
        set => this.SetProperty(ref _selectedPin, value);
    }

    private CompileSignalModel _selectedSignal;

    public FpgaModel(string name)
    {
        Name = name;
    }

    public override string ToString()
    {
        return Name;
    }
}