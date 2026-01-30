using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public abstract class FpgaGuiElementViewModelBase : ObservableObject
{
    public FpgaGuiElementViewModelBase(double x, double y)
    {
        X = x;
        Y = y;
    }

    public IHardwareModel? Parent { get; init; }

    public double X { get; }

    public double Y { get; }

    public double Rotation { get; init; }

    public virtual void Initialize()
    {
    }
}