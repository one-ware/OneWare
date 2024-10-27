using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPinBlockViewModel : FpgaGuiElementInterfaceViewModelBase
{
    public double Width { get; init; }
    public FpgaGuiElementPinViewModel[]? Pins { get; init; } 
    
    public FpgaGuiElementPinBlockViewModel(double x, double y) : base(x, y)
    {
    }
    
    public override void Initialize()
    {
        base.Initialize();
        
        if(Pins == null) return;
        foreach (var pin in Pins)
        {
            pin.Initialize();
        }
    }
}