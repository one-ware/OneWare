using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPinViewModel : FpgaGuiElementRectViewModel
{
    private const int DefaultWidth = 10;

    private const int DefaultHeight = 10;

    public FpgaPinModel? PinModel { get; init; }

    public FpgaGuiElementPinViewModel(int x, int y, int width, int height) : base(x, y,
        width == 0 ? DefaultWidth : width, height == 0 ? DefaultHeight : height)
    {
    }
}