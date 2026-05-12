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
    /// Returns true if the given string looks like an old-format HDL file path (with known HDL extension).
    /// </summary>
    private static bool IsLegacyTopEntityFilePath(string value) =>
        value.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) ||
        value.EndsWith(".vhdl", StringComparison.OrdinalIgnoreCase) ||
        value.EndsWith(".v", StringComparison.OrdinalIgnoreCase) ||
        value.EndsWith(".sv", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The name of the top-level HDL entity/module (e.g. "blink_top").
    /// </summary>
    public string? TopEntity
    {
        get
        {
            var value = Properties.GetString("topEntity");
            // Backward compat: old format stored an HDL file path here
            if (value != null && IsLegacyTopEntityFilePath(value))
                return Path.GetFileNameWithoutExtension(value);
            return value;
        }
        set => Properties.SetString("topEntity", value);
    }

    /// <summary>
    /// The relative path to the file containing the top-level entity/module.
    /// </summary>
    public string? TopEntityFile
    {
        get
        {
            var topEntityFile = Properties.GetString("topEntityFile");
            if (topEntityFile != null) return topEntityFile;

            // Backward compat: old format stored an HDL file path in "topEntity"
            var topEntity = Properties.GetString("topEntity");
            if (topEntity != null && IsLegacyTopEntityFilePath(topEntity))
                return topEntity.ToUnixPath();

            return null;
        }
        set => Properties.SetString("topEntityFile", value?.ToUnixPath());
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
