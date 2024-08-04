using Avalonia.Media;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementImageViewModel : FpgaGuiElementRectViewModel
{
    public IImage? Image { get; init; }
    
    public FpgaGuiElementImageViewModel(int x, int y, int width, int height) : base(x, y, width, height)
    {
        
    }
}