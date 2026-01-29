using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public abstract class FpgaViewModelBase : ObservableObject, IDisposable
{
    public FpgaViewModelBase(FpgaModel fpgaModel)
    {
        FpgaModel = fpgaModel;
    }

    public FpgaModel FpgaModel { get; }

    public virtual void Dispose()
    {
    }
}