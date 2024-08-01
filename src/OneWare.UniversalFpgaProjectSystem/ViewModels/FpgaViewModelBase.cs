using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public abstract class FpgaViewModelBase : ObservableObject
{
    public FpgaModel FpgaModel { get; }
    
    public FpgaViewModelBase(FpgaModel fpgaModel)
    {
        FpgaModel = fpgaModel;
    }
}