using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class HardwareGuiViewModel : ObservableObject
{
    public int Width { get; set; }

    public int Height { get; set; }

    public List<FpgaGuiElementViewModelBase> Elements { get; set; } = new();
}