using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.IasCameraExtension.ViewModels;

public class IasCameraExtensionViewModel : FpgaExtensionModel
{
    public IasCameraExtensionViewModel(IFpgaExtension fpgaExtension) : base(fpgaExtension)
    {
    }
}