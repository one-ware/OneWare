using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga.Gui;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementViewModelBase : ObservableObject
{
    public FpgaGuiElement Element { get; }

    public FpgaGuiElementViewModelBase(FpgaGuiElement element)
    {
        Element = element;
    }
}