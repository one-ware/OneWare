namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementChildViewModel : FpgaGuiElementViewModelBase
{
    public FpgaGuiElementChildViewModel(double x, double y) : base(x, y)
    {
    }

    public HardwareGuiViewModel? Child { get; init; }

    public override void Initialize()
    {
        base.Initialize();

        Child?.Initialize();
    }
}