using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementCruviHsViewModel : FpgaGuiElementRectViewModel
{
    private const int DefaultWidth = 80;

    private const int DefaultHeight = 20;

    public FpgaInterfaceModel? InterfaceModel { get; init; }

    public FpgaGuiElementCruviHsViewModel(int x, int y, int width, int height, IBrush color) : base(x, y,
        width == 0 ? DefaultWidth : width, height == 0 ? DefaultHeight : height, color)
    {
    }
}