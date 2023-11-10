using System.Collections.ObjectModel;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class FpgaService
{
    public ObservableCollection<FpgaModelBase> FpgaModels { get; } = new();
    
    public void AddFpga(FpgaModelBase fpgaModelBase)
    {
        FpgaModels.Add(fpgaModelBase);
    }
}