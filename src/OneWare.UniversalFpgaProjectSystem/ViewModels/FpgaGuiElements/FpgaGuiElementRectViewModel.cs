using Avalonia;
using Avalonia.Media;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementRectViewModel : FpgaGuiElementTextViewModel
{
    public FpgaGuiElementRectViewModel(double x, double y, double width, double height) : base(x, y)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }

    public IBrush? Color { get; set; }

    public CornerRadius CornerRadius { get; init; }

    public BoxShadows BoxShadow { get; init; }
}