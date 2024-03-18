using OneWare.Essentials.Services;
using OneWare.PackageManager.Serializer;

namespace OneWare.PackageManager.Models;

public class NativeToolPackageModel(Package package, IHttpService httpService, ILogger logger, IPaths paths) 
    : PackageModel(package, "NativeTool", Path.Combine(paths.NativeToolsDirectory, package.Id!), httpService, logger)
{
    protected override void Install()
    {
        
    }

    protected override void Uninstall()
    {
        
    }
}