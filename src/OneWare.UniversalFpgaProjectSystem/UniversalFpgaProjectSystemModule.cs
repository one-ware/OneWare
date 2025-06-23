// OneWare.UniversalFpgaProjectSystem/UniversalFpgaProjectSystemModule.cs
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using System;


namespace OneWare.UniversalFpgaProjectSystem;

// The class itself doesn't need to implement IModule explicitly in your snippet,
// but for Prism to find it, it usually does. Assuming it implicitly does or is configured.
public class UniversalFpgaProjectSystemModule : IModule // Explicitly implement for clarity
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<UniversalFpgaProjectManager>();
        containerRegistry.RegisterSingleton<FpgaService>();

        containerRegistry.RegisterSingleton<UniversalFpgaProjectToolBarViewModel>();
        containerRegistry.Register<UniversalFpgaProjectTestBenchToolBarViewModel>(); // Register the VM itself

        // === Keep the Func factory registration as is, this is Prism's way ===
        // Within Prism's IContainerRegistry, this is the standard way to provide
        // a factory that uses the container to create an instance with parameters.
        // The `Resolve` here is within the *framework's* factory generation lambda,
        // not your core application logic.
        containerRegistry.Register<Func<IFile, UniversalFpgaProjectTestBenchToolBarViewModel>>(containerProvider =>
        {
            return new Func<IFile, UniversalFpgaProjectTestBenchToolBarViewModel>(file =>
                containerProvider.Resolve<UniversalFpgaProjectTestBenchToolBarViewModel>(
                    (typeof(IFile), file)));
        });
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        // Still no direct Resolve calls in OnInitialized itself.
        containerProvider.Resolve<UniversalFpgaProjectSystemModuleInitializer>().Initialize();
    }
}