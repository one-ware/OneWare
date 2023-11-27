using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using OneWare.ProjectSystem.Models;
using OneWare.SDK.Converters;
using OneWare.SDK.Helpers;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class UniversalFpgaProjectRoot : UniversalProjectRoot
{
    public const string ProjectFileExtension = ".fpgaproj";
    public const string ProjectType = "UniversalFPGAProject";
    
    public override string ProjectTypeId => ProjectType;

    private readonly IImage _topEntityOverlay;

    private IProjectEntry? _topEntity;

    public IProjectEntry? TopEntity
    {
        get => _topEntity;
        set
        {
            _topEntity?.IconOverlays.Remove(_topEntityOverlay);
            SetProperty(ref _topEntity, value);
            _topEntity?.IconOverlays.Add(_topEntityOverlay);

            if (_topEntity != null)
                Properties[nameof(TopEntity)] = _topEntity.RelativePath;
            else
                Properties.Remove(nameof(TopEntity));
        }
    }

    private IFpgaToolchain? _toolchain;
    
    public IFpgaToolchain? Toolchain
    {
        get => _toolchain;
        set
        {
            SetProperty(ref _toolchain, value);
            if (_toolchain != null)
                Properties[nameof(Toolchain)] = _toolchain.Name;
            else
                Properties.Remove(nameof(Toolchain));
        }
    }
    
    private IFpgaLoader? _loader;
    
    public IFpgaLoader? Loader
    {
        get => _loader;
        set
        {
            SetProperty(ref _loader, value);
            if (_loader != null)
                Properties[nameof(Loader)] = _loader.Name;
            else
                Properties.Remove(nameof(Loader));
        }
    }

    public UniversalFpgaProjectRoot(string projectFilePath, JsonObject properties) 
        : base(projectFilePath, properties)
    {
        _topEntityOverlay = Application.Current!.FindResource(ThemeVariant.Dark, "VsImageLib2019.DownloadOverlay16X") as IImage 
                            ?? throw new NullReferenceException("TopEntity Icon");
        
        Icon = SharedConverters.PathToBitmapConverter.Convert(ContainerLocator.Container.Resolve<IPaths>().AppIconPath, typeof(Bitmap), null, null) as Bitmap;
    }
}