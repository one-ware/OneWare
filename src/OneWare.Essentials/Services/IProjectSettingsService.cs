using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IProjectSettingsService
{
    public void AddProjectSetting(string key, TitledSetting projectSetting);
    
    public void AddProjectSetting(ProjectSetting projectSetting);

    public T[] GetComboOptions<T>(string key);

    public void Load(string path);
    
    public void Save(string path);
    
    public Dictionary<string, ProjectSetting> GetProjectSettingsDictionary();
}