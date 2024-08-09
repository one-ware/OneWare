using Avalonia.Layout;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPinArrayViewModel : FpgaGuiElementViewModelBase
{
    public bool IsHorizontal { get; init; }
    public Orientation Orientation => IsHorizontal ? Orientation.Horizontal : Orientation.Vertical;
    public FpgaGuiElementPinViewModel[]? Pins { get; init; } 
    
    public FpgaGuiElementPinArrayViewModel(double x, double y) : base(x, y)
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