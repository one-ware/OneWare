using Avalonia.Platform.Storage;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface ISettingsService
{
    public void RegisterSettingCategory(string category, int priority = 0, string? iconKey = null);

    public void RegisterSettingSubCategory(string category, string subCategory, int priority = 0,
        string? iconKey = null);

    public void Register<T>(string key, T defaultValue);

    public IObservable<T> Bind<T>(string key, IObservable<T> observable);

    public void RegisterTitled<T>(string category, string subCategory, string key, string title, string description,
        T defaultValue);

    [Obsolete("Use RegisterTitledFolderPath instead")]
    public void RegisterTitledPath(string category, string subCategory, string key, string title, string description,
        string defaultValue, string? watermark, string? startDir, Func<string, bool>? validate);
    
    public void RegisterTitledFolderPath(string category, string subCategory, string key, string title, string description,
        string defaultValue, string? watermark, string? startDir, Func<string, bool>? validate);
    
    public void RegisterTitledFilePath(string category, string subCategory, string key, string title, string description,
        string defaultValue, string? watermark, string? startDir, Func<string, bool>? validate, params FilePickerFileType[] fileTypes);

    public void RegisterTitledSlider<T>(string category, string subCategory, string key, string title, string description,
        T defaultValue, double min, double max, double step);

    public void RegisterTitledCombo<T>(string category, string subCategory, string key, string title,
        string description,
        T defaultValue, params T[] options);
    
    public void RegisterTitledComboSearch<T>(string category, string subCategory, string key, string title,
        string description,
        T defaultValue, params T[] options);

    public void RegisterTitledListBox(string category, string subCategory, string key, string title,
        string description, params string[] defaultValue);

    public void RegisterCustom(string category, string subCategory, string key, CustomSetting customSetting);

    public T GetSettingValue<T>(string key);

    public T[] GetComboOptions<T>(string key);

    public void SetSettingValue(string key, object value);

    public IObservable<T> GetSettingObservable<T>(string key);

    public void Load(string path);

    public void Save(string path);

    public void WhenLoaded(Action action);

    public void Reset(string key);

    public void ResetAll();
}