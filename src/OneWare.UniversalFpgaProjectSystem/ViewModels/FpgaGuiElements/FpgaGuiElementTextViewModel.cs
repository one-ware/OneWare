using Avalonia.Media;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementTextViewModel : FpgaGuiElementViewModelBase
{
    private IBrush? _foreground;

    public FpgaGuiElementTextViewModel(double x, double y) : base(x, y)
    {
    }

    public string? Text { get; init; }

    public IBrush? Foreground
    {
        get => _foreground;
        set => SetProperty(ref _foreground, value);
    }

    public int FontSize { get; init; } = 10;

    public FontWeight FontWeight { get; init; }
}