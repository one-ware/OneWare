using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IProjectSettingsService
{
    public void AddProjectSetting(string key, TitledSetting projectSetting);
    
    public void AddProjectSetting(ProjectSetting projectSetting);

    public void Load(string path);
    
    public void Save(string path);
    
    public List<ProjectSetting> GetProjectSettingsList();
}