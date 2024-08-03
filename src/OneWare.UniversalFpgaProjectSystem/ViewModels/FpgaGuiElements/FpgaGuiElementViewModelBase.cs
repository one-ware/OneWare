using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga.Gui;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementViewModelBase : ObservableObject
{
    public int X { get; }
    
    public int Y { get; }
    
    public int Width { get; }
    
    public int Height { get; }

    public FpgaGuiElementViewModelBase(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}