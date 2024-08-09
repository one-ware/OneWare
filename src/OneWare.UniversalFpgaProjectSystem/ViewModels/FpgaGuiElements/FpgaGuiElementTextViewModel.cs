using Avalonia.Media;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementTextViewModel : FpgaGuiElementViewModelBase
{
    public string? Text { get; init; }

    private IBrush? _foreground;
    
    public IBrush? Foreground
    {
        get => _foreground;
        set => SetProperty(ref _foreground, value);
    }

    public int FontSize { get; init; } = 10;
    
    public FontWeight FontWeight { get; init; }
    
    public FpgaGuiElementTextViewModel(double x, double y) : base(x, y)
    {
       
    }
}