using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class ProjectSettingsService : IProjectSettingsService
{
    public List<ProjectSetting> ProjectSettings { get; } = new();
    
    public void AddProjectSetting(string key, TitledSetting projectSetting, Func<IProjectRootWithFile, bool> activationFunction)
    {
        AddProjectSetting(new ProjectSetting(key, projectSetting, activationFunction));
    }
    
    public void AddProjectSetting(ProjectSetting projectSetting)
    {
        ProjectSettings.Add(projectSetting);
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