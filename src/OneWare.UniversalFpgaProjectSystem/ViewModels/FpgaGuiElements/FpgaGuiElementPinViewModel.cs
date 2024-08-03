using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Fpga.Gui;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPinViewModel : FpgaGuiElementViewModelBase
{
    private const int DefaultWidth = 10;

    private const int DefaultHeight = 10;
    
    public int Width { get; }
    
    public int Height { get; }
    
    public FpgaPinModel? PinModel { get; set; }
    
    public IBrush Color { get; set; }

    public FpgaGuiElementPinViewModel(int x, int y, int width, int height, FpgaPinModel? pin, IBrush color) : base(x, y) 
    {
        PinModel = pin;
        Color = color;

        Width = width == 0 ? DefaultWidth : width;
        Height = height == 0 ? DefaultHeight : height;
    }
}