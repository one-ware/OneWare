using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public abstract class ExtensionViewModelBase : ObservableObject, IDisposable
{
    public ExtensionModel ExtensionModel { get; }
    
    public ExtensionViewModelBase(ExtensionModel extensionModel)
    {
        ExtensionModel = extensionModel;
    }

    public virtual void Initialize()
    {
        
    }
    
    public virtual void Dispose()
    {
        
    }
}