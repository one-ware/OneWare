using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public abstract class ExtensionViewModelBase : ObservableObject, IDisposable
{
    public ExtensionViewModelBase(ExtensionModel extensionModel)
    {
        ExtensionModel = extensionModel;
    }

    public ExtensionModel ExtensionModel { get; }

    public virtual void Dispose()
    {
    }

    public virtual void Initialize()
    {
    }
}