using Avalonia.Media.Imaging;
using OneWare.ProjectSystem.Models;
using OneWare.Shared.Converters;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Parser;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class UniversalFpgaProjectRoot : ProjectRoot
{
    public const string ProjectFileExtension = ".fpgaproj";
    public const string ProjectType = "UniversalFPGAProject";
    public override string ProjectPath => ProjectFilePath;
    public override string ProjectTypeId => ProjectType;
    public string ProjectFilePath { get; }
    
    public FpgaProjectProperties Properties { get; }

    public UniversalFpgaProjectRoot(string projectFilePath, FpgaProjectProperties properties) : base(Path.GetDirectoryName(projectFilePath) ?? throw new NullReferenceException("Invalid Project Path"))
    {
        ProjectFilePath = projectFilePath;
        Properties = properties;
        
        Icon = SharedConverters.PathToBitmapConverter.Convert(ContainerLocator.Container.Resolve<IPaths>().AppIconPath, typeof(Bitmap), null, null) as Bitmap;
    }
}