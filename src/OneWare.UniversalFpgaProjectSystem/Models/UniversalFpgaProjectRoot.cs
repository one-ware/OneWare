using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.ProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class UniversalFpgaProjectRoot : UniversalProjectRoot
{
    public const string ProjectFileExtension = ".fpgaproj";
    public const string ProjectType = "UniversalFPGAProject";

    private readonly ObservableCollection<IProjectEntry> _compileExcluded = [];

    private readonly ObservableCollection<IFpgaPreCompileStep> _preCompileSteps = [];

    private readonly ObservableCollection<IProjectFile> _testBenches = [];

    private readonly IImage _testBenchOverlay;

    private readonly IImage _topEntityOverlay;

    public UniversalFpgaProjectRoot(string projectFilePath, JsonObject properties)
        : base(projectFilePath, properties)
    {
        _topEntityOverlay =
            Application.Current!.FindResource(ThemeVariant.Dark, "VsImageLib2019.DownloadOverlay16X") as IImage
            ?? throw new NullReferenceException(nameof(Application));

        _testBenchOverlay = Application.Current!.FindResource(ThemeVariant.Dark, "TestBenchOverlay") as IImage
                            ?? throw new NullReferenceException(nameof(Application));

        TestBenches = new ReadOnlyObservableCollection<IProjectFile>(_testBenches);
        CompileExcluded = new ReadOnlyObservableCollection<IProjectEntry>(_compileExcluded);
        PreCompileSteps = new ReadOnlyObservableCollection<IFpgaPreCompileStep>(_preCompileSteps);
    }

    public override string ProjectTypeId => ProjectType;

    public IProjectEntry? TopEntity
    {
        get;
        set
        {
            field?.IconOverlays.Remove(_topEntityOverlay);
            SetProperty(ref field, value);
            field?.IconOverlays.Add(_topEntityOverlay);

            if (field != null)
                SetProjectProperty(nameof(TopEntity), field.RelativePath.ToUnixPath());
            else
                RemoveProjectProperty(nameof(TopEntity));
        }
    }

    public IFpgaToolchain? Toolchain
    {
        get;
        set
        {
            SetProperty(ref field, value);
            if (field != null)
                SetProjectProperty(nameof(Toolchain), field.Name);
            else
                RemoveProjectProperty(nameof(Toolchain));
        }
    }

    public IFpgaLoader? Loader
    {
        get;
        set
        {
            SetProperty(ref field, value);
            if (field != null)
                SetProjectProperty(nameof(Loader), field.Name);
            else
                RemoveProjectProperty(nameof(Loader));
        }
    }

    public ReadOnlyObservableCollection<IProjectFile> TestBenches { get; }

    public ReadOnlyObservableCollection<IProjectEntry> CompileExcluded { get; }

    public ReadOnlyObservableCollection<IFpgaPreCompileStep> PreCompileSteps { get; }

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
            if (!await step.PerformPreCompileStepAsync(this, fpga))
                return;
        await Toolchain.CompileAsync(this, fpga);
    }
}