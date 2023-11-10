using System.Collections.ObjectModel;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class FpgaService
{
    public ObservableCollection<FpgaModel> FpgaModels { get; } = new();
    
    public void AddFpga(FpgaModel fpgaModel)
    {
        FpgaModels.Add(fpgaModel);
    }
}