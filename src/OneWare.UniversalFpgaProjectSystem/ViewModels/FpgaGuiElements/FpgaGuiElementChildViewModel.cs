using Avalonia.Layout;
using OneWare.UniversalFpgaProjectSystem.Helpers;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementChildViewModel : FpgaGuiElementViewModelBase
{
    public HardwareGuiViewModel? Child { get; init; }
    
    public FpgaGuiElementChildViewModel(double x, double y) : base(x, y)
    {
    }

    public override void Initialize()
    {
        base.Initialize();

        Child?.Initialize();
    }
}