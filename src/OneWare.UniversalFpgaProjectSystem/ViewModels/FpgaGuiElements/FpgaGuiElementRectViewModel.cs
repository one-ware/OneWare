using Avalonia;
using Avalonia.Media;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementRectViewModel : FpgaGuiElementViewModelBase
{
    
    public int Width { get; }
    
    public int Height { get; }
    
    public IBrush? Color { get; set; }

    public CornerRadius CornerRadius { get; init; }

    public BoxShadows BoxShadow { get; init; }
    
    public FpgaGuiElementRectViewModel(int x, int y, int width, int height) : base(x, y) 
    {
        Width = width;
        Height = height;
    }
}