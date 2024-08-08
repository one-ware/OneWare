using System.Collections.ObjectModel;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class FpgaService
{
    private readonly ILogger _logger;

    public FpgaService(IPaths paths, ILogger logger)
    {
        HardwareDirectory = Path.Combine(paths.PackagesDirectory, "Hardware");
        Directory.CreateDirectory(HardwareDirectory);
        
        //Create Local
        Directory.CreateDirectory(Path.Combine(HardwareDirectory, "Local"));
        Directory.CreateDirectory(Path.Combine(HardwareDirectory, "Local", "FPGA"));
        Directory.CreateDirectory(Path.Combine(HardwareDirectory, "Local", "Extensions"));
        
        _logger = logger;
    }

    public string HardwareDirectory { get; }

    public Dictionary<string, Type> NodeProviders { get; } = new();

    public ObservableCollection<IFpgaPackage> FpgaPackages { get; } = new();

    public ObservableCollection<IFpgaExtensionPackage> FpgaExtensionPackages { get; } = new();

    public ObservableCollection<IFpgaToolchain> Toolchains { get; } = new();

    public ObservableCollection<IFpgaLoader> Loaders { get; } = new();

    public ObservableCollection<IFpgaSimulator> Simulators { get; } = new();

    public ObservableCollection<IFpgaProjectTemplate> Templates { get; } = new();

    public ObservableCollection<IFpgaPreCompileStep> PreCompileSteps { get; } = new();

    public void RegisterFpgaPackage(IFpgaPackage fpga)
    {
        FpgaPackages.InsertSorted(fpga, (x1, x2) => string.Compare(x1.Name, x2.Name, StringComparison.Ordinal));
    }

    public void RegisterFpgaExtensionPackage(IFpgaExtensionPackage fpgaExtension)
    {
        var existing = FpgaExtensionPackages.FirstOrDefault(x => x.Name == fpgaExtension.Name);

        //Allow overwrite from folder
        if (existing != null) FpgaExtensionPackages.Remove(existing);

        FpgaExtensionPackages.InsertSorted(fpgaExtension,
            (x1, x2) => string.Compare(x1.Name, x2.Name, StringComparison.Ordinal));
    }

    public void RegisterNodeProvider<T>(params string[] extensions) where T : INodeProvider
    {
        foreach (var ext in extensions) NodeProviders[ext] = typeof(T);
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

    public void LoadGenericHardware()
    {
        foreach (var fpga in FpgaPackages.ToArray())
        {
            if (fpga is GenericFpgaPackage)
            {
                FpgaPackages.Remove(fpga);
            }
        }

        foreach (var extension in FpgaExtensionPackages.ToArray())
        {
            if (extension is GenericFpgaExtensionPackage gen && !gen.PackagePath.StartsWith("avares://"))
            {
                FpgaExtensionPackages.Remove(extension);
            }
        }

        try
        {
            foreach (var packageDir in Directory.GetDirectories(HardwareDirectory))
            {
                var fpgaDir = Path.Combine(packageDir, "FPGA");

                if (Directory.Exists(fpgaDir))
                    foreach (var fpga in Directory.GetDirectories(fpgaDir))
                    {
                        RegisterFpgaPackage(new GenericFpgaPackage(Path.GetFileName(fpga), fpga));
                    }
                
                var extensionDir = Path.Combine(packageDir, "Extensions");
                
                if (Directory.Exists(extensionDir))
                    foreach (var connector in Directory.GetDirectories(extensionDir))
                    {
                        foreach (var extension in Directory.GetDirectories(connector))
                        {
                            RegisterFpgaExtensionPackage(new GenericFpgaExtensionPackage(Path.GetFileName(extension), Path.GetFileName(connector), extension));
                        }
                    }
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}