using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementCruviLsViewModel : FpgaGuiElementViewModelBase
{
    public HardwareInterfaceModel? InterfaceModel { get; init; }

    public FpgaGuiElementCruviLsViewModel(int x, int y) : base(x, y)
    {
    }
}