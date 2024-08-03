using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Fpga.Gui;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPinViewModel : FpgaGuiElementViewModelBase
{
    private const int DefaultWidth = 10;

    private const int DefaultHeight = 10;
    
    public FpgaPinModel? PinModel { get; set; }
    
    public IBrush Color { get; set; }

    public FpgaGuiElementPinViewModel(int x, int y, int width, int height, FpgaPinModel? pin, IBrush color) : base(x, y,
        width == 0 ? DefaultWidth : width, height == 0 ? DefaultHeight : height) 
    {
        PinModel = pin;
        Color = color;
    }
}