using System.Collections.ObjectModel;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class FpgaService
{
    private ObservableCollection<Type> FpgaModels { get; } = new();

    public ObservableCollection<IFpgaToolchain> FpgaToolchains { get; } = new();
    
    public ObservableCollection<IFpgaLoader> FpgaLoaders { get; } = new();
    
    public void AddFpga<T>() where T : FpgaModelBase
    {
        FpgaModels.Add(typeof(T));
    }

    public IEnumerable<FpgaModelBase> GetFpgas()
    {
        foreach (var t in FpgaModels)
        {
            yield return Activator.CreateInstance(t) as FpgaModelBase;
        }
    }
}