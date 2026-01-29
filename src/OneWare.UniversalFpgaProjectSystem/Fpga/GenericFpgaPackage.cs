using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class GenericFpgaPackage(string name, string packagePath) : IFpgaPackage
{
    public string Name { get; } = name;

    public IFpga LoadFpga()
    {
        var fpgaFile = Path.Combine(packagePath, "fpga.json");
        if (File.Exists(fpgaFile)) return new GenericFpga(Name, fpgaFile);
        throw new Exception("fpga.json not found");
    }

    public FpgaViewModelBase? LoadFpgaViewModel(FpgaModel fpgaModel)
    {
        var guiFile = Path.Combine(packagePath, "gui.json");
        return File.Exists(guiFile) ? new GenericFpgaViewModel(fpgaModel, guiFile) : null;
    }

    public override string ToString()
    {
        return Name;
    }
}