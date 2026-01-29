namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPinBlockViewModel : FpgaGuiElementInterfaceViewModelBase
{
    public FpgaGuiElementPinBlockViewModel(double x, double y) : base(x, y)
    {
    }

    public double Width { get; init; }
    public FpgaGuiElementPinViewModel[]? Pins { get; init; }

    public override void Initialize()
    {
        base.Initialize();

        if (Pins == null) return;
        foreach (var pin in Pins) pin.Initialize();
    }
}