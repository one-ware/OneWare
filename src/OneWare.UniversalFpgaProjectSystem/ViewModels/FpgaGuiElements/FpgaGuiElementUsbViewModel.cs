using Avalonia.Layout;
using OneWare.UniversalFpgaProjectSystem.Helpers;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementUsbViewModel : FpgaGuiElementViewModelBase
{
    public FpgaGuiElementPinViewModel? RxPin { get; private set; } 
    
    public FpgaGuiElementPinViewModel? TxPin { get; private set; } 
    
    public string? BindRx { get; init; }
    
    public string? BindTx { get; init; }
    
    public FpgaGuiElementUsbViewModel(double x, double y) : base(x, y)
    {
    }

    public override void Initialize()
    {
        base.Initialize();

        RxPin ??= new FpgaGuiElementPinViewModel(0, 0, 12, 10)
        {
            Bind = BindRx,
            Color = HardwareGuiCreator.ColorShortcuts["RX"],
            Text = "RX",
            Rotation = -90,
            Parent = Parent,
            FlipLabel = true
        };
        
        TxPin ??= new FpgaGuiElementPinViewModel(0, 0, 12, 10)
        {
            Bind = BindTx,
            Color = HardwareGuiCreator.ColorShortcuts["TX"],
            Text = "TX",
            Rotation = -90,
            Parent = Parent,
            FlipLabel = true
        };
        
        RxPin.Initialize();
        TxPin.Initialize();
    }
}