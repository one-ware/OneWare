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
    
    public int FontSize { get; }
    
    public FontWeight FontWeight { get; }
    
    public FpgaGuiElementTextViewModel(int x, int y, string text, IBrush? color, int fontSize, FontWeight fontWeight) : base(x, y)
    {
        Text = text;
        Color = color;
        FontSize = fontSize > 0 ? fontSize : 12;
        FontWeight = fontWeight;
    }
}