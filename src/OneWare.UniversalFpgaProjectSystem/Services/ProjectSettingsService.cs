using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class ProjectSettingsService : IProjectSettingsService
{
    public List<ProjectSetting> ProjectSettings { get; } = new();
    private Dictionary<string, List<ProjectSetting>> ProjectSettingsByCategory { get; } = new();
    
    /// <inheritdoc/>
    public void AddProjectSetting(string key, TitledSetting projectSetting, Func<IProjectRootWithFile, bool> activationFunction)
    {
        AddProjectSetting(new ProjectSetting(key, projectSetting, activationFunction));
    }
    
    /// <inheritdoc/>
    public void AddProjectSetting(ProjectSetting projectSetting)
    {
        ProjectSettings.Add(projectSetting);

        if (!ProjectSettingsByCategory.TryGetValue(projectSetting.Category, out List<ProjectSetting>? value))
        {
            value = [];
            ProjectSettingsByCategory[projectSetting.Category] = value;
        }

        value.Add(projectSetting);
    }

    public void AddProjectSettingIfNotExists(ProjectSetting projectSetting)
    {
        if (ProjectSettings.All(x => x.Key != projectSetting.Key))
        {
            AddProjectSetting(projectSetting);
        }
    }

    /// <inheritdoc/>
    public List<string> GetProjectCategories()
    {
        return ProjectSettingsByCategory.Keys.ToList();
    }
    
    /// <inheritdoc/>
    public List<ProjectSetting> GetProjectSettingsList(string category)
    {
        return ProjectSettingsByCategory[category];
    }

    public string GetDefaultProjectCategory()
    {
        return "General";
    }

    public void Load(string path)
    {
        throw new NotImplementedException();
    }

    public void Save(string path)
    {
        throw new NotImplementedException();
    }

    public List<ProjectSetting> GetProjectSettingsList()
    {
        List<ProjectSetting> ret = new();
        
        foreach (var projectSetting in ProjectSettings)
        {
            ret.Add(new ProjectSetting(projectSetting.Key, projectSetting.Setting.Clone(), projectSetting.ActivationFunction));
        }
        
        return ret;
    }
    
    
}