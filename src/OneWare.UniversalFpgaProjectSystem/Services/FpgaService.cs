using System.Collections.ObjectModel;
using NodeEditor.Controls;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class FpgaService
{
    public Dictionary<IFpga, Type> CustomFpgaModels { get; } = new();
    public Dictionary<string, Type> NodeProviders { get; } = new();
    public ObservableCollection<IFpga> Fpgas { get; } = new();
    public ObservableCollection<IFpgaToolchain> Toolchains { get; } = new();
    public ObservableCollection<IFpgaLoader> Loaders { get; } = new();
    
    public void RegisterFpga(IFpga fpga)
    {
        Fpgas.Add(fpga);
    }
    
    public void RegisterNodeProvider<T>(params string[] extensions) where T : INodeProvider
    {
        foreach (var ext in extensions)
        {
            NodeProviders[ext] = typeof(T);
        }
    }

    public void RegisterCustomFpgaModel<T>(IFpga fpga) where T : FpgaModel
    {
        CustomFpgaModels.Add(fpga, typeof(T));
    }
    
    public void RegisterToolchain<T>() where T : IFpgaToolchain
    {
        Toolchains.Add(ContainerLocator.Container.Resolve<T>());
    }
    
    public void RegisterLoader<T>() where T : IFpgaLoader
    {
        Loaders.Add(ContainerLocator.Container.Resolve<T>());
    }

    public INodeProvider? GetNodeProvider(string extension)
    {
        NodeProviders.TryGetValue(extension, out var provider);
        if (provider != null)
        {
            return ContainerLocator.Container.Resolve(provider) as INodeProvider;
        }
        return null;
    }
}