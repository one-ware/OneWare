using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Parser;

public static class UniversalFpgaProjectParser
{
    public static UniversalFpgaProjectRoot? Deserialize(string path)
    {
        try
        {
            using var file = File.OpenRead(path);

            return new UniversalFpgaProjectRoot(path);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return null;
        }
    }

    public static bool Serialize(UniversalFpgaProjectRoot root)
    {
        try
        {
            using var file = File.Create(root.ProjectFilePath);
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }
}