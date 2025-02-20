using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class ProjectSettingsService : IProjectSettingsService
{
    public Dictionary<string, TitledSetting> ProjectSettings { get; } = new();
    
    public void AddProjectSetting(string key, TitledSetting projectSetting)
    {
        AddProjectSetting(new ProjectSetting(key, projectSetting));
    }
    
    public void AddProjectSetting(ProjectSetting projectSetting)
    {
        ProjectSettings.Add(projectSetting.Key, projectSetting.Setting);
    }

    public T[] GetComboOptions<T>(string key)
    {
        throw new NotImplementedException();
    }

    public void Load(string path)
    {
        throw new NotImplementedException();
    }

    public void Save(string path)
    {
        throw new NotImplementedException();
    }

    public Dictionary<string, ProjectSetting> GetProjectSettingsDictionary()
    {
        throw new NotImplementedException();
    }
}