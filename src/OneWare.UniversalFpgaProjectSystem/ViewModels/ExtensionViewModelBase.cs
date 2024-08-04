using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public abstract class ExtensionViewModelBase : ObservableObject, IDisposable
{
    public FpgaExtensionModel ExtensionModel { get; }
    
    public ExtensionViewModelBase(FpgaExtensionModel extensionModel)
    {
        ExtensionModel = extensionModel;
    }

    public virtual void Dispose()
    {
        
    }
}