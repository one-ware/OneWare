using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class GenericFpgaExtensionPackage(string name, string connector, string packagePath) : IFpgaExtensionPackage
{
    public string Name { get; } = name;
    public string Connector { get; } = connector;
    public string PackagePath { get; } = packagePath;
    
    public IFpgaExtension LoadExtension()
    {
        var extensionFile = Path.Combine(PackagePath, "extension.json");
        if (File.Exists(extensionFile) || extensionFile.StartsWith("avares://"))
        {
            return new GenericFpgaExtension(Name, Connector, extensionFile);
        }
        throw new Exception("fpga.json not found");
    }

    public ExtensionViewModelBase? LoadExtensionViewModel(ExtensionModel extensionModel)
    {
        var guiFile = Path.Combine(PackagePath, "gui.json");
        return File.Exists(guiFile) || guiFile.StartsWith("avares://") ? new GenericExtensionViewModel(extensionModel, guiFile) : null;
    }
}