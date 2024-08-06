using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class GenericFpgaExtensionPackage(string name, string connector, string packagePath) : IFpgaExtensionPackage
{
    public string Name { get; } = name;
    public string Connector { get; } = connector;
    
    public IFpgaExtension LoadExtension()
    {
        var extensionFile = Path.Combine(packagePath, "extension.json");
        if (File.Exists(extensionFile))
        {
            return new GenericFpgaExtension(Name, Connector, extensionFile);
        }
        throw new Exception("fpga.json not found");
    }

    public ExtensionViewModelBase? LoadExtensionViewModel(ExtensionModel extensionModel)
    {
        var guiFile = Path.Combine(packagePath, "gui.json");
        return File.Exists(guiFile) ? new GenericExtensionViewModel(extensionModel, guiFile) : null;
    }
}