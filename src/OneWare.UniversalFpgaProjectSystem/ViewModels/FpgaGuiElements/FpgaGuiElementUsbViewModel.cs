using Avalonia.Layout;
using OneWare.UniversalFpgaProjectSystem.Helpers;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementUsbViewModel : FpgaGuiElementViewModelBase
{
    public FpgaGuiElementPinViewModel? RxPin { get; private set; } 
    
    public FpgaGuiElementPinViewModel? TxPin { get; private set; } 
    
    public bool FlipLabel { get; init; }
    
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
            Rotation = FlipLabel ? 90 : -90,
            Parent = Parent,
            LabelPosition = !FlipLabel ? PinLabelPosition.After : PinLabelPosition.Before,
        };
        
        TxPin ??= new FpgaGuiElementPinViewModel(0, 0, 12, 10)
        {
            Bind = BindTx,
            Color = HardwareGuiCreator.ColorShortcuts["TX"],
            Text = "TX",
            Rotation = FlipLabel ? 90 : -90,
            Parent = Parent,
            LabelPosition = !FlipLabel ? PinLabelPosition.After : PinLabelPosition.Before,
        };
        
        RxPin.Initialize();
        TxPin.Initialize();
    }
}