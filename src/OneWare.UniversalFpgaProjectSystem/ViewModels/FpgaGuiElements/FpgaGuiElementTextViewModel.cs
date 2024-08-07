using Avalonia.Media;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementTextViewModel : FpgaGuiElementViewModelBase
{
    public string Text { get; }

    private IBrush? _color;
    
    public IBrush? Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public int FontSize { get; init; } = 10;
    
    public FontWeight FontWeight { get; init; }
    
    public FpgaGuiElementTextViewModel(int x, int y, string text) : base(x, y)
    {
        Text = text;
    }
}