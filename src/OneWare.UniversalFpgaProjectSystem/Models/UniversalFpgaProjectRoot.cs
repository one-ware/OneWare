using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OneWare.Essentials.Extensions;
using OneWare.ProjectSystem.Models;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class UniversalFpgaProjectRoot : UniversalProjectRoot
{
    public const string ProjectFileExtension = ".fpgaproj";
    public const string ProjectType = "UniversalFPGAProject";
    
    public override string ProjectTypeId => ProjectType;

    private readonly IImage _topEntityOverlay;
    private readonly IImage _testBenchOverlay;

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
                SetProjectProperty(nameof(TopEntity), _topEntity.RelativePath.ToUnixPath());
            else
                RemoveProjectProperty(nameof(TopEntity));
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
                SetProjectProperty(nameof(Toolchain), _toolchain.Name);
            else
                RemoveProjectProperty(nameof(Toolchain));
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
                SetProjectProperty(nameof(Loader), _loader.Name);
            else
                RemoveProjectProperty(nameof(Loader));
        }
    }

    private readonly ObservableCollection<IProjectFile> _testBenches = [];

    public ReadOnlyObservableCollection<IProjectFile> TestBenches { get; }
    
    
    private readonly ObservableCollection<IProjectEntry> _compileExcluded = [];

    public ReadOnlyObservableCollection<IProjectEntry> CompileExcluded { get; }
    
    
    private readonly ObservableCollection<IFpgaPreCompileStep> _preCompileSteps = [];
    
    public ReadOnlyObservableCollection<IFpgaPreCompileStep> PreCompileSteps { get; }

    public UniversalFpgaProjectRoot(string projectFilePath, JsonObject properties) 
        : base(projectFilePath, properties)
    {
        _topEntityOverlay = Application.Current!.FindResource(ThemeVariant.Dark, "VsImageLib2019.DownloadOverlay16X") as IImage 
                            ?? throw new NullReferenceException(nameof(Application));
        
        _testBenchOverlay = Application.Current!.FindResource(ThemeVariant.Dark, "TestBenchOverlay") as IImage 
                            ?? throw new NullReferenceException(nameof(Application));

        TestBenches = new ReadOnlyObservableCollection<IProjectFile>(_testBenches);
        CompileExcluded = new ReadOnlyObservableCollection<IProjectEntry>(_compileExcluded);
        PreCompileSteps = new ReadOnlyObservableCollection<IFpgaPreCompileStep>(_preCompileSteps);
    }

    protected override IProjectFolder ConstructNewProjectFolder(string path, IProjectFolder topFolder)
    {
        return new FpgaProjectFolder(path, topFolder);
    }

    protected override IProjectFile ConstructNewProjectFile(string path, IProjectFolder topFolder)
    {
        return new FpgaProjectFile(path, topFolder);
    }

    public void RegisterTestBench(IProjectFile file)
    {
        _testBenches.Add(file);
        file.IconOverlays.Add(_testBenchOverlay);
        SetProjectPropertyArray(nameof(TestBenches), TestBenches.Select(x => x.RelativePath.ToUnixPath()));
    }
    
    public void UnregisterTestBench(IProjectFile file)
    {
        _testBenches.Remove(file);
        file.IconOverlays.Remove(_testBenchOverlay);
        SetProjectPropertyArray(nameof(TestBenches), TestBenches.Select(x => x.RelativePath.ToUnixPath()));
    }

    public void RegisterCompileExcluded(IProjectEntry entry)
    {
        _compileExcluded.Add(entry);
        entry.TextOpacity = 0.5f;
        SetProjectPropertyArray(nameof(CompileExcluded), CompileExcluded.Select(x => x.RelativePath.ToUnixPath()));
    }

    public void UnregisterCompileExcluded(IProjectEntry entry)
    {
        _compileExcluded.Remove(entry);
        entry.TextOpacity = 1f;
        SetProjectPropertyArray(nameof(CompileExcluded), CompileExcluded.Select(x => x.RelativePath.ToUnixPath()));
    }
    
    public void RegisterPreCompileStep(IFpgaPreCompileStep step)
    {
        _preCompileSteps.Add(step);
        SetProjectPropertyArray(nameof(PreCompileSteps), PreCompileSteps.Select(x => x.Name));
    }

    public void UnregisterPreCompileStep(IFpgaPreCompileStep step)
    {
        _preCompileSteps.Remove(step);
        SetProjectPropertyArray(nameof(PreCompileSteps), PreCompileSteps.Select(x => x.Name));
    }

    public async Task RunToolchainAsync(FpgaModel fpga)
    {
        if (Toolchain == null) return;
        
        foreach (var step in PreCompileSteps)
        {
            if(!await step.PerformPreCompileStepAsync(this, fpga)) return;
        }
        await Toolchain.CompileAsync(this, fpga);
    }
}