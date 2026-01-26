using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.Essentials.Services;

namespace OneWare.OssCadSuiteIntegration.Tools;

public class YosysSettingHelper
{
    private static readonly IImage? _icon = Application.Current!.FindResource(ThemeVariant.Dark, "ForkAwesome.Check") as IImage;
    
    public static Task UpdateProjectPcFile(IProjectFile file)
    {
        if (file.Root is not UniversalFpgaProjectRoot universalFpgaProjectRoot)
            return Task.CompletedTask;

        var path = GetConstraintFile(universalFpgaProjectRoot);
        foreach (var projectFile in file.Root.Files)
        {
            if (_icon != null) projectFile.IconOverlays.Remove(_icon);
        }

        if (_icon != null && !file.IconOverlays.Contains(_icon))
            file.IconOverlays.Add(_icon);

        if (file.RelativePath == path)
            return Task.CompletedTask;

        UpdateProjectProperties(universalFpgaProjectRoot, file.RelativePath);
        return ContainerLocator.Container.Resolve<UniversalFpgaProjectManager>()
            .SaveProjectAsync(universalFpgaProjectRoot);
    }

    public static void SetConstraintOverlay(IProjectRoot project)
    {
        if (project is not UniversalFpgaProjectRoot  universalFpgaProjectRoot) 
            return;
        
        var path = GetConstraintFile(universalFpgaProjectRoot);
        foreach (var projectFile in universalFpgaProjectRoot.Files)
        {
            if (_icon != null) projectFile.IconOverlays.Remove(_icon);
        }
        
        foreach (var projectFile in universalFpgaProjectRoot.Files)
        {
            if (projectFile.RelativePath.Equals(path))
            {
                projectFile.IconOverlays.Add(_icon!);
                return;
            }
        }
    }

    public static string GetConstraintFile(UniversalFpgaProjectRoot project)
    {
        if (!HasProjectProperties(project)) return "project.pcf";

        var path = project.Properties["OSS_CAD"]?.AsObject()?["ConstraintFile"]?.ToString();
        return path ?? "project.pcf";
    }
    
    public static void UpdateProjectProperties(UniversalFpgaProjectRoot project, string? constraintFile)
    {
        bool ccfInclude = true;
        var test = project.Properties["Include"]?.AsArray()!;
        foreach (var t in test)
        {
            if (t.ToString() == "*.pcf")
                ccfInclude = false;
        }

        if (ccfInclude)
        {

            project.Properties["Include"]?.AsArray().Add("*.pcf");
        }

        JsonNode js = new JsonObject();
        if (constraintFile != null)
        {
            js["ConstraintFile"] = constraintFile;
        }
        else
            js["ConstraintFile"] = "project.pcf";

        project.Properties["OSS_CAD"] = js;
    }
    
    public static bool HasProjectProperties(UniversalFpgaProjectRoot project)
    {
        if (project.Properties.ContainsKey("OSS_CAD"))
        {
            if (project.Properties["OSS_CAD"]?.AsObject().ContainsKey("ConstraintFile") ?? false)
            {
                return true;
            }
        }

        return false;
    }
}