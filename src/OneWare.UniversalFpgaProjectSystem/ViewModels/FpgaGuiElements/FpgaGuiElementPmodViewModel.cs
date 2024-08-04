using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPmodViewModel : FpgaGuiElementViewModelBase
{
    public FpgaInterfaceModel? InterfaceModel { get; init; }

    public FpgaGuiElementPmodViewModel(int x, int y) : base(x, y)
    {
    }
}