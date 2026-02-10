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
    
    public UniversalFpgaProjectRoot(string projectFilePath) : base(projectFilePath)
    {
        RegisterProjectEntryModification(x =>
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
        
        RegisterProjectEntryModification(x =>
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
    }

    public override string ProjectTypeId => ProjectType;

    public string? TopEntity
    {
        get => Properties.GetString("topEntity");
        set => Properties.SetString("topEntity", value?.ToUnixPath());
    }

    public string? Toolchain
    {
        get => Properties.GetString("toolchain");
        set => Properties.SetString("toolchain", value);
    }

    public string? Loader
    {
        get => Properties.GetString("loader");
        set => Properties.SetString("loader", value);
    }

    public bool IsTestBench(string relativePath)
    {
        return Properties.IsIncludedPathHelper(relativePath, "testBenches");
    }

    public void AddTestBench(string relativePath)
    {
        Properties.AddIncludedPathHelper(relativePath, "testBenches");
    }

    public void RemoveTestBench(string relativePath)
    {
        Properties.RemoveIncludedPathHelper(relativePath, "testBenches");
    }

    public bool IsCompileExcluded(string relativePath)
    {
        return Properties.IsIncludedPathHelper(relativePath, "compileExcluded");
    }

    public void AddCompileExcluded(string relativePath)
    {
        Properties.AddIncludedPathHelper(relativePath, "compileExcluded");
    }

    public void RemoveCompileExcluded(string relativePath)
    {
        Properties.RemoveIncludedPathHelper(relativePath, "compileExcluded");
    }

    protected override IProjectFolder ConstructNewProjectFolder(string path, IProjectFolder topFolder)
    {
        return new FpgaProjectFolder(path, topFolder);
    }

    protected override IProjectFile ConstructNewProjectFile(string path, IProjectFolder topFolder)
    {
        return new FpgaProjectFile(path, topFolder);
    }
}
