using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public abstract class FpgaGuiElementViewModelBase : ObservableObject
{
    public int X { get; }
    
    public int Y { get; }

    public double Rotation { get; init; }

    public FpgaGuiElementViewModelBase(int x, int y)
    {
        X = x;
        Y = y;
    }
}