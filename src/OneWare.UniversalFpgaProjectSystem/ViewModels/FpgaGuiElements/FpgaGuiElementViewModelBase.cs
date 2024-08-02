using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga.Gui;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementViewModelBase : ObservableObject
{
    public FpgaModel Model { get; }
    public FpgaGuiElement Element { get; }

    public FpgaGuiElementViewModelBase(FpgaModel model, FpgaGuiElement element)
    {
        Model = model;
        Element = element;
    }
}