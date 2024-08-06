using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class HardwareGuiViewModel(IHardwareModel parent) : ObservableObject
{
    public IHardwareModel Parent
    {
        get => parent;
        set => SetProperty(ref parent, value);
    }
    
    public int Width { get; set; }

    public int Height { get; set; }
    
    public Thickness Margin { get; set; }
    
    public List<FpgaGuiElementViewModelBase> Elements { get; set; } = new();
}