using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.OssCadSuiteIntegration.Tools;

public class YosysSettingHelper
{
    public static Task UpdateProjectPcFileAsync(IProjectRoot root, IProjectFile? file)
    {
        if (root is not UniversalFpgaProjectRoot universalFpgaProjectRoot)
            return Task.CompletedTask;

        var path = GetConstraintFile(universalFpgaProjectRoot);

        if (file?.RelativePath == path)
            return Task.CompletedTask;

        UpdateProjectProperties(universalFpgaProjectRoot, file?.RelativePath);
        return ContainerLocator.Container.Resolve<UniversalFpgaProjectManager>()
            .SaveProjectAsync(universalFpgaProjectRoot);
    }

    public static string GetConstraintFile(UniversalFpgaProjectRoot project)
    {
        return project.Properties.GetString("ossCad/constraintFile") ?? "project.pcf";
    }
    
    public static void UpdateProjectProperties(UniversalFpgaProjectRoot project, string? constraintFile)
    {
        var include = project.Properties.GetStringArray("include");
        var hasPcfInclude = false;
        if (include != null)
        {
            foreach (var entry in include)
            {
                if (entry == "*.pcf")
                {
                    hasPcfInclude = true;
                    break;
                }
            }
        }

        if (!hasPcfInclude)
            project.Properties.AddToStringArray("include", "*.pcf");

        project.Properties.SetString("ossCad/constraintFile", constraintFile);
    }
}
