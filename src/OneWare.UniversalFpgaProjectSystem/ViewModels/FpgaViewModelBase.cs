using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public abstract class FpgaViewModelBase : ObservableObject, IDisposable
{
    public FpgaModel FpgaModel { get; }
    
    public FpgaViewModelBase(FpgaModel fpgaModel)
    {
        FpgaModel = fpgaModel;
    }

    public virtual void Dispose()
    {
        
    }
}