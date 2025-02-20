using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class ProjectSettingService : IProjectSettingsService
{
    public Dictionary<string, ProjectSetting> ProjectSettings { get; } = new();
    
    public void AddProjectSetting(string key, TitledSetting projectSetting)
    {
        throw new NotImplementedException();
    }
    
    public void AddProjectSetting(ProjectSetting projectSetting)
    {
        throw new NotImplementedException();
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
}