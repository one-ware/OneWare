using Avalonia;
using Avalonia.Media;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementEllipseViewModel : FpgaGuiElementViewModelBase
{
    public double Width { get; }
    
    public double Height { get; }
    
    public IBrush Color { get; }
    
    public FpgaGuiElementEllipseViewModel(double x, double y, double width, double height, IBrush color) : base(x, y) 
    {
        Color = color;

        Width = width;
        Height = height;
    }
}