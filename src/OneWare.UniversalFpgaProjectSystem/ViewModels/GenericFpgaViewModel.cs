using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class GenericFpgaViewModel : FpgaViewModelBase
{
    public GenericFpgaViewModel(FpgaModel fpgaModel, string guiPath) : base(fpgaModel)
    {
        
    }
}