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
        // "fpga" was the legacy key for the board; alias it so old plugins that call
        // Properties.GetString("fpga") continue to receive the migrated "board" value.
        Properties.RegisterAlias("fpga", "board");

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
            if (x is IProjectFile file && file.RelativePath.EqualPaths(TopEntityFile))
            {
                x.Icon?.AddOverlay("TopEntity", "VsImageLib2019.DownloadOverlay16X");
            }
            else
            {
                x.Icon?.RemoveOverlay("TopEntity");
            }
        });
        
        RegisterProjectEntryModification(x =>
        {
            if (x is IProjectFile file && IsCompileExcluded(file.RelativePath))
            {
                x.TextOpacity = 0.5f;
            }
            else
            {
                x.TextOpacity = 1.0f;
            }
        });
    }

    public override string ProjectTypeId => ProjectType;

    /// <summary>
    /// The relative path to the file containing the top-level entity/module.
    /// Stored as <c>topEntityFile</c>. Old files using <c>topEntity</c> as a file path
    /// are migrated automatically on load.
    /// </summary>
    public string? TopEntityFile
    {
        get => Properties.GetString("topEntityFile");
        set => Properties.SetString("topEntityFile", value?.ToUnixPath());
    }

    /// <summary>
    /// The name of the top-level entity or module within <see cref="TopEntityFile"/>.
    /// Stored as <c>topEntity</c>.
    /// </summary>
    public string? TopEntity
    {
        get => Properties.GetString("topEntity");
        set => Properties.SetString("topEntity", value);
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

    /// <summary>
    /// The selected hardware board (evaluation board) for this project.
    /// Stored as <c>board</c> in the project file; old files using <c>fpga</c> are migrated automatically.
    /// </summary>
    public string? Board
    {
        get => Properties.GetString("board");
        set => Properties.SetString("board", value);
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
