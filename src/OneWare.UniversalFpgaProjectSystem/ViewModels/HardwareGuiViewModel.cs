using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class HardwareGuiViewModel : ObservableObject
{
    private readonly List<FpgaGuiElementViewModelBase> _elements = [];
    
    public IReadOnlyList<FpgaGuiElementViewModelBase> Elements { get; }
    
    public int Width { get; set; }

    public int Height { get; set; }
    
    public Thickness Margin { get; set; }

    public HardwareGuiViewModel()
    {
        Elements = _elements.AsReadOnly();
    }
    
    public void AddElement(FpgaGuiElementViewModelBase element)
    {
        _elements.Add(element);
    }

    public void Initialize()
    {
        foreach (var element in Elements)
        {
            element.Initialize();
        }
    }
}