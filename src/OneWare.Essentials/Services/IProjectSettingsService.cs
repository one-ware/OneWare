using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IProjectSettingsService
{
    [Obsolete("Use AddProjectSetting(ProjectSetting projectSetting) instead.")]
    public void AddProjectSetting(string key, TitledSetting projectSetting, Func<IProjectRootWithFile, bool> activationFunction);
    
    /// <summary>
    /// Adds an existing <see cref="ProjectSetting"/> instance.
    /// </summary>
    /// <param name="projectSetting">The project setting to add.</param>
    public void AddProjectSetting(ProjectSetting projectSetting);

    public void AddProjectSettingIfNotExists(ProjectSetting projectSetting);
    
    public void Load(string path);
    
    public void Save(string path);
    
    public List<ProjectSetting> GetProjectSettingsList();
    
    /// <summary>
    /// Gets a list of all available project setting categories.
    /// </summary>
    /// <returns>A list of category names.</returns>
    public List<string> GetProjectCategories();

    /// <summary>
    /// Gets a list of project settings belonging to the specified category.
    /// </summary>
    /// <param name="category">The category name to filter settings.</param>
    /// <returns>A list of <see cref="ProjectSetting"/> instances in the specified category.</returns>
    public List<ProjectSetting> GetProjectSettingsList(string category);
    
    /// <summary>
    /// Returns the default category name used when no specific category is assigned to a project setting.
    /// </summary>
    /// <returns>The default category name as a string.</returns>
    public string GetDefaultProjectCategory();
    
}