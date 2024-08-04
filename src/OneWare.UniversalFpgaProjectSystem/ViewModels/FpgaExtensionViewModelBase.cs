using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public abstract class FpgaExtensionViewModelBase : ObservableObject, IDisposable
{
    public FpgaExtensionModel ExtensionModel { get; }
    
    public FpgaExtensionViewModelBase(FpgaExtensionModel extensionModel)
    {
        ExtensionModel = extensionModel;
    }

    public virtual void Dispose()
    {
        
    }
}