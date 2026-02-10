using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
    
    public UniversalFpgaProjectRoot(string projectFilePath) : base(projectFilePath)
    {
        RegisterEntryModification(x =>
        {
            if (x is IProjectFile file && IsTestBench(file.RelativePath))
            {
                x.Icon?.AddOverlay("TestBench", "TestBenchOverlay");
            }
            else
            {
                x.Icon?.RemoveOverlay("TestBench");
            }
        });
        
        RegisterEntryModification(x =>
        {
            if (x is IProjectFile file && TopEntity == file.RelativePath)
            {
                x.Icon?.AddOverlay("TopEntity", "VsImageLib2019.DownloadOverlay16X");
            }
            else
            {
                x.Icon?.RemoveOverlay("TopEntity");
            }
        });

        PreCompileSteps = new ReadOnlyObservableCollection<IFpgaPreCompileStep>(_preCompileSteps);
    }

    public override string ProjectTypeId => ProjectType;

    public string? TopEntity
    {
        get => Properties.GetString(nameof(TopEntity));
        set => Properties.SetString(nameof(TopEntity), value?.ToUnixPath());
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

    // private sealed class TestBenchOverlayProvider(UniversalFpgaProjectRoot root, IImage overlayImage)
    //     : IProjectEntryOverlayProvider
    // {
    //     public IEnumerable<IconLayer> GetOverlays(IProjectEntry entry)
    //     {
    //         if (entry is IProjectRoot) return Array.Empty<IconLayer>();
    //         if (!root.IsTestBench(entry.RelativePath)) return Array.Empty<IconLayer>();
    //
    //         return
    //         [
    //             new IconLayer
    //             {
    //                 Icon = overlayImage,
    //                 HorizontalAlignment = HorizontalAlignment.Right,
    //                 VerticalAlignment = VerticalAlignment.Bottom,
    //                 SizeRatio = 1
    //             }
    //         ];
    //     }
    // }
}
