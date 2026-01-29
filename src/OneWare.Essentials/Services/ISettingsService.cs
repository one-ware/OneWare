using Avalonia.Platform.Storage;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public class SaveEventArgs(bool autoSave) : EventArgs
{
    public bool AutoSave { get; } = autoSave;
}

public interface ISettingsService
{
    public event EventHandler<SaveEventArgs>? Saved;

    public void RegisterSettingCategory(string category, int priority = 0, string? iconKey = null);

    public void RegisterSettingSubCategory(string category, string subCategory, int priority = 0,
        string? iconKey = null);

    public void Register<T>(string key, T defaultValue);

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

    public void RegisterSetting(string cateogory, string subCategory, string key, TitledSetting setting);

    public void UpdateSetting(string key, TitledSetting setting);

    public void RegisterCustom(string category, string subCategory, string key, CustomSetting customSetting);

    public Setting GetSetting(string key);

    public bool HasSetting(string key);

    public T GetSettingValue<T>(string key);

    public T[] GetComboOptions<T>(string key);

    public void SetSettingValue(string key, object value);

    public IObservable<T> GetSettingObservable<T>(string key);

    public void Load(string path);

    public void Save(string path, bool autoSave = true);

    public void WhenLoaded(Action action);

    public void Reset(string key);

    public void ResetAll();
}