using Avalonia.Platform.Storage;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public class SaveEventArgs(bool autoSave) : EventArgs
{
    public bool AutoSave { get; } = autoSave;
}

public interface ISettingsService
{
    /// <summary>
    /// Fired after settings are saved.
    /// </summary>
    public event EventHandler<SaveEventArgs>? Saved;

    /// <summary>
    /// Registers a top-level settings category.
    /// </summary>
    public void RegisterSettingCategory(string category, int priority = 0, string? iconKey = null);

    /// <summary>
    /// Registers a settings sub-category.
    /// </summary>
    public void RegisterSettingSubCategory(string category, string subCategory, int priority = 0,
        string? iconKey = null);

    /// <summary>
    /// Registers a simple setting with a default value.
    /// </summary>
    public void Register<T>(string key, T defaultValue);

    /// <summary>
    /// Binds a setting to an observable and returns the bound observable.
    /// </summary>
    public IObservable<T> Bind<T>(string key, IObservable<T> observable);

    [Obsolete("Use RegisterSetting instead")]
    public void RegisterTitled<T>(string category, string subCategory, string key, string title, string description,
        T defaultValue);

    [Obsolete("Use RegisterSetting instead")]
    public void RegisterTitledFolderPath(string category, string subCategory, string key, string title,
        string description,
        string defaultValue, string? watermark, string? startDir, Func<string, bool>? validate);

    [Obsolete("Use RegisterSetting instead")]
    public void RegisterTitledFilePath(string category, string subCategory, string key, string title,
        string description,
        string defaultValue, string? watermark, string? startDir, Func<string, bool>? validate,
        params FilePickerFileType[] fileTypes);

    [Obsolete("Use RegisterSetting instead")]
    public void RegisterTitledSlider(string category, string subCategory, string key, string title, string description,
        double defaultValue, double min, double max, double step);

    [Obsolete("Use RegisterSetting instead")]
    public void RegisterTitledCombo<T>(string category, string subCategory, string key, string title,
        string description,
        T defaultValue, params T[] options);

    [Obsolete("Use RegisterSetting instead")]
    public void RegisterTitledComboSearch<T>(string category, string subCategory, string key, string title,
        string description,
        T defaultValue, params T[] options);

    [Obsolete("Use RegisterSetting instead")]
    public void RegisterTitledListBox(string category, string subCategory, string key, string title,
        string description, params string[] defaultValue);

    /// <summary>
    /// Registers a titled setting in the specified category.
    /// </summary>
    public void RegisterSetting(string cateogory, string subCategory, string key, TitledSetting setting);

    /// <summary>
    /// Updates a titled setting definition.
    /// </summary>
    public void UpdateSetting(string key, TitledSetting setting);

    /// <summary>
    /// Registers a custom setting UI definition.
    /// </summary>
    public void RegisterCustom(string category, string subCategory, string key, CustomSetting customSetting);

    /// <summary>
    /// Returns a setting by key.
    /// </summary>
    public Setting GetSetting(string key);

    /// <summary>
    /// Returns true if the setting exists.
    /// </summary>
    public bool HasSetting(string key);

    /// <summary>
    /// Returns the current value of a setting.
    /// </summary>
    public T GetSettingValue<T>(string key);

    /// <summary>
    /// Returns options for a combo setting.
    /// </summary>
    public T[] GetComboOptions<T>(string key);

    /// <summary>
    /// Sets the value of a setting.
    /// </summary>
    public void SetSettingValue(string key, object value);

    /// <summary>
    /// Returns an observable for setting changes.
    /// </summary>
    public IObservable<T> GetSettingObservable<T>(string key);

    /// <summary>
    /// Loads settings from a file.
    /// </summary>
    public void Load(string path);

    /// <summary>
    /// Saves settings to a file.
    /// </summary>
    public void Save(string path, bool autoSave = true);

    /// <summary>
    /// Runs an action once settings are loaded.
    /// </summary>
    public void WhenLoaded(Action action);

    /// <summary>
    /// Resets a setting to its default value.
    /// </summary>
    public void Reset(string key);

    /// <summary>
    /// Resets all settings to defaults.
    /// </summary>
    public void ResetAll();
}
