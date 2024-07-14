using System.Collections.ObjectModel;
using OneWare.Essentials.Extensions;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class FpgaService
{
    public Dictionary<IFpga, Type> CustomFpgaViewModels { get; } = new();

    public Dictionary<IFpgaExtension, Type> FpgaExtensionViewModels { get; } = new();

    public Dictionary<string, Type> NodeProviders { get; } = new();

    public ObservableCollection<IFpga> Fpgas { get; } = new();

    public ObservableCollection<IFpgaExtension> FpgaExtensions { get; } = new();

    public ObservableCollection<IFpgaToolchain> Toolchains { get; } = new();

    public ObservableCollection<IFpgaLoader> Loaders { get; } = new();

    public ObservableCollection<IFpgaSimulator> Simulators { get; } = new();

    public ObservableCollection<IFpgaProjectTemplate> Templates { get; } = new();

    public ObservableCollection<IFpgaPreCompileStep> PreCompileSteps { get; } = new();

    public void RegisterFpga(IFpga fpga)
    {
        Fpgas.InsertSorted(fpga, (x1, x2) => string.Compare(x1.Name, x2.Name, StringComparison.Ordinal));
    }

    public void RegisterFpgaExtension(IFpgaExtension fpga)
    {
        FpgaExtensions.InsertSorted(fpga, (x1, x2) => string.Compare(x1.Name, x2.Name, StringComparison.Ordinal));
    }

    public void RegisterNodeProvider<T>(params string[] extensions) where T : INodeProvider
    {
        foreach (var ext in extensions) NodeProviders[ext] = typeof(T);
    }

    public void RegisterCustomFpgaViewModel<T>(IFpga fpga) where T : FpgaModel
    {
        CustomFpgaViewModels.Add(fpga, typeof(T));
    }

    public void RegisterFpgaExtensionViewModel<T>(IFpgaExtension fpgaExtension) where T : FpgaExtensionModel
    {
        FpgaExtensionViewModels.Add(fpgaExtension, typeof(T));
    }

    public void RegisterToolchain<T>() where T : IFpgaToolchain
    {
        Toolchains.Add(ContainerLocator.Container.Resolve<T>());
    }

    public void RegisterLoader<T>() where T : IFpgaLoader
    {
        Loaders.Add(ContainerLocator.Container.Resolve<T>());
    }

    public void RegisterSimulator<T>() where T : IFpgaSimulator
    {
        Simulators.Add(ContainerLocator.Container.Resolve<T>());
    }

    public void RegisterTemplate<T>() where T : IFpgaProjectTemplate
    {
        Templates.Add(ContainerLocator.Container.Resolve<T>());
    }

    public void RegisterPreCompileStep<T>() where T : IFpgaPreCompileStep
    {
        PreCompileSteps.Add(ContainerLocator.Container.Resolve<T>());
    }

    public IFpgaPreCompileStep? GetPreCompileStep(string name)
    {
        return PreCompileSteps.FirstOrDefault(x => x.Name == name);
    }

    public INodeProvider? GetNodeProvider(string extension)
    {
        NodeProviders.TryGetValue(extension, out var provider);
        if (provider != null) return ContainerLocator.Container.Resolve(provider) as INodeProvider;
        return null;
    }
}