using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPmodViewModel : FpgaGuiElementRectViewModel
{
    private const int DefaultWidth = 90;

    private const int DefaultHeight = 30;

    public FpgaInterfaceModel? InterfaceModel { get; init; }

    public FpgaGuiElementPmodViewModel(int x, int y, int width, int height, IBrush color) : base(x, y,
        width == 0 ? DefaultWidth : width, height == 0 ? DefaultHeight : height, color)
    {
    }
}