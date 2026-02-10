using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class ProjectSettingsService : IProjectSettingsService
{
    public List<ProjectSetting> ProjectSettings { get; } = new();
    private Dictionary<string, List<ProjectSetting>> ProjectSettingsByCategory { get; } = new();
    
    /// <inheritdoc />
    public void AddProjectSetting(ProjectSetting projectSetting)
    {
        ProjectSettings.Add(projectSetting);

        if (!ProjectSettingsByCategory.TryGetValue(projectSetting.Category, out var value))
        {
            value = [];
            ProjectSettingsByCategory[projectSetting.Category] = value;
        }

        value.Add(projectSetting);
    }

    public void AddProjectSettingIfNotExists(ProjectSetting projectSetting)
    {
        if (ProjectSettings.All(x => x.Key != projectSetting.Key)) AddProjectSetting(projectSetting);
    }

    /// <inheritdoc />
    public List<string> GetProjectCategories()
    {
        return ProjectSettingsByCategory.Keys.ToList();
    }

    /// <inheritdoc />
    public List<ProjectSetting> GetProjectSettingsList(string category)
    {
	    return ProjectSettingsByCategory[category];
    }

    public string GetDefaultProjectCategory()
    {
        return "General";
    }

    public List<ProjectSetting> GetProjectSettingsList()
    {
        return ProjectSettings;
    }
}