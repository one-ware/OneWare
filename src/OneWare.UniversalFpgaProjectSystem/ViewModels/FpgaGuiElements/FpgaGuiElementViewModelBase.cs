using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public abstract class FpgaGuiElementViewModelBase : ObservableObject
{
    public IHardwareModel? Parent { get; init; }
    
    public int X { get; }
    
    public int Y { get; }

    public double Rotation { get; init; }

    public FpgaGuiElementViewModelBase(int x, int y)
    {
        X = x;
        Y = y;
    }
    
    public virtual void Initialize()
    {
    }
}