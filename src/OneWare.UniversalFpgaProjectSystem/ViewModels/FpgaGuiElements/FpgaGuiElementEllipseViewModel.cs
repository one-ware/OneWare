using Avalonia;
using Avalonia.Media;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementEllipseViewModel : FpgaGuiElementViewModelBase
{
    
    public int Width { get; }
    
    public int Height { get; }
    
    public IBrush Color { get; }
    
    public FpgaGuiElementEllipseViewModel(int x, int y, int width, int height, IBrush color) : base(x, y) 
    {
        Color = color;

        Width = width;
        Height = height;
    }
}