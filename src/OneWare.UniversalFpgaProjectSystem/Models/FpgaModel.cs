using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaModel : ObservableObject
{
    public Dictionary<string, FpgaPinModel> AvailablePins { get; } = new();
    
    private FpgaPinModel? _selectedPinModel;
    public FpgaPinModel? SelectedPinModel
    {
        get => _selectedPinModel;
        set => SetProperty(ref _selectedPinModel, value);
    }

    private NodeModel? _selectedNode;
    public NodeModel? SelectedNode
    {
        get => _selectedNode;
        set => SetProperty(ref _selectedNode, value);
    }
    
    public Control? UserInterface { get; }
    
    public string Name { get; }

    public FpgaModel(string name, Control? userInterface = null)
    {
        Name = name;
        UserInterface = userInterface;
    }

    public void SelectPin(FpgaPinModel model)
    {
        SelectedPinModel = model;
    }

    public override string ToString()
    {
        return Name;
    }
}