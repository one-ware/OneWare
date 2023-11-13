using System.Collections.ObjectModel;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class FpgaService
{
    public ObservableCollection<Type> FpgaModels { get; } = new();
    
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