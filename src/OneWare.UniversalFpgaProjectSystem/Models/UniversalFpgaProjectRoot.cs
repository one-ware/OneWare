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

    private readonly ObservableCollection<IFpgaPreCompileStep> _preCompileSteps = [];

    private readonly IImage _testBenchOverlay;

    private readonly IImage _topEntityOverlay;

    public UniversalFpgaProjectRoot(string projectFilePath) : base(projectFilePath)
    {
        _topEntityOverlay =
            Application.Current!.FindResource(ThemeVariant.Dark, "VsImageLib2019.DownloadOverlay16X") as IImage
            ?? throw new NullReferenceException(nameof(Application));

        _testBenchOverlay = Application.Current!.FindResource(ThemeVariant.Dark, "TestBenchOverlay") as IImage
                            ?? throw new NullReferenceException(nameof(Application));

        PreCompileSteps = new ReadOnlyObservableCollection<IFpgaPreCompileStep>(_preCompileSteps);
    }

    public override string ProjectTypeId => ProjectType;

    public string? TopEntity
    {
        get => GetProjectProperty(nameof(TopEntity));
        set => SetProjectProperty(nameof(TopEntity), value?.ToUnixPath());
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

    public ReadOnlyObservableCollection<IFpgaPreCompileStep> PreCompileSteps { get; }

    public bool IsTestBench(string relativePath)
    {
        return IsIncludedPathHelper(relativePath, "testBenches");
    }

    public void AddTestBench(string relativePath)
    {
        AddIncludedPathHelper(relativePath, "testBenches");
    }

    public void RemoveTestBench(string relativePath)
    {
        RemoveIncludedPathHelper(relativePath, "testBenches");
    }

    public bool IsCompileExcluded(string relativePath)
    {
        return IsIncludedPathHelper(relativePath, "compileExcluded");
    }

    public void AddCompileExcluded(string relativePath)
    {
        AddIncludedPathHelper(relativePath, "compileExcluded");
    }

    public void RemoveCompileExcluded(string relativePath)
    {
        RemoveIncludedPathHelper(relativePath, "compileExcluded");
    }

    protected override IProjectFolder ConstructNewProjectFolder(string path, IProjectFolder topFolder)
    {
        return new FpgaProjectFolder(path, topFolder);
    }

    protected override IProjectFile ConstructNewProjectFile(string path, IProjectFolder topFolder)
    {
        return new FpgaProjectFile(path, topFolder);
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