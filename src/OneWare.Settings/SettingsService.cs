using System.Reactive.Linq;
using System.Text.Json;
using DynamicData.Binding;
using OneWare.SDK.Services;

namespace OneWare.Settings;

public class SettingsService : ISettingsService
{
    public Dictionary<string, SettingCategory> SettingCategories { get; } = new();
    public Dictionary<string, Setting> Settings { get; } = new();

    private readonly List<Action> _afterLoadingActions = new();

    public void RegisterSettingCategory(string category, int priority = 0, string? iconKey = null)
    {
        SettingCategories.TryAdd(category, new SettingCategory());
        SettingCategories[category].IconKey = iconKey;
        SettingCategories[category].Priority = priority;
    }
    
    public void RegisterSettingSubCategory(string category, string subCategory, int priority = 0, string? iconKey = null)
    {
        SettingCategories.TryAdd(category, new SettingCategory());
        SettingCategories[category].SettingSubCategories.TryAdd(subCategory, new SettingSubCategory());
        SettingCategories[category].SettingSubCategories[subCategory].Priority = priority;
        SettingCategories[category].SettingSubCategories[subCategory].IconKey = iconKey;
    }
    
    public void Register<T>(string key, T defaultValue)
    {
        if (defaultValue == null) throw new NullReferenceException(nameof(defaultValue));
        Settings.Add(key, new Setting(defaultValue));
    }

    public IObservable<T> Bind<T>(string key, IObservable<T> observable)
    {
        if(!Settings.TryGetValue(key, out var setting)) throw new ArgumentException($"Setting {key} is not registered!");;
        observable.Subscribe(x => setting.Value = x!);
        return GetSettingObservable<T>(key);
    }

    public void RegisterTitled<T>(string category, string subCategory, string key, string title, string description, T defaultValue)
    {
         if (defaultValue == null) throw new NullReferenceException(nameof(defaultValue));
        AddSetting(category, subCategory, key, new TitledSetting(title, description, defaultValue));
    }

    public void RegisterTitledPath(string category, string subCategory, string key, string title, string description,
        string defaultValue, string? watermark, string? startDir, Func<string, bool>? validate)
    {
        AddSetting(category, subCategory, key, new FolderPathSetting(title, description, defaultValue, watermark,startDir, validate));
    }

    public void RegisterTitledCombo<T>(string category, string subCategory, string key, string title, string description, T defaultValue, params T[] options)
    {
        if (defaultValue == null) throw new NullReferenceException(nameof(defaultValue));
        AddSetting(category, subCategory, key, new ComboBoxSetting(title, description, defaultValue, options.Cast<object>()));
    }

    private void AddSetting(string category, string subCategory, string key, TitledSetting setting)
    {
        Settings.Add(key, setting);
        SettingCategories.TryAdd(category, new SettingCategory());
        var cat = SettingCategories[category];
        cat.SettingSubCategories.TryAdd(subCategory, new SettingSubCategory());
        var sub = cat.SettingSubCategories[subCategory];
        sub.Settings.Add(setting);
    }
    
    public T GetSettingValue<T>(string key)
    {
        Settings.TryGetValue(key, out var value);
        if(value?.Value is T) return (T)Convert.ChangeType(value.Value, typeof(T));
        throw new ArgumentException($"Setting {key} is not registered!");
    }

    public T[] GetComboOptions<T>(string key)
    {
        Settings.TryGetValue(key, out var value);
        if (value is ComboBoxSetting cs && value?.Value is T)
        {
            var destinationArray = new T[cs.Options.Length];
            Array.Copy(cs.Options, destinationArray, cs.Options.Length);
            return destinationArray;
        }
        throw new ArgumentException($"Setting {key} is not registered!");
    }

    public void SetSettingValue(string key, object value)
    {
        Settings.TryGetValue(key, out var s);
        if (s == null) throw new Exception($"Error setting Setting: {key} does not exist!");
        s.Value = value;
    }
    
    public IObservable<T> GetSettingObservable<T>(string key)
    {
        Settings.TryGetValue(key, out var value);
        if (value is Setting {Value: T} setting)
        {
            return setting.WhenValueChanged(x => x.Value)!.Cast<T>();
        }
        throw new ArgumentException($"Setting {key} is not registered!");
    }

    public void Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var stream = File.OpenRead(path);
            var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(stream, new JsonSerializerOptions()
            {
                AllowTrailingCommas = true
            });
            if (settings == null) return;
            foreach (var setting in settings)
            {
                try
                {
                    if (Settings.TryGetValue(setting.Key, out var setting1))
                    {
                        if (setting.Value is JsonElement je)
                            setting1.Value = je.Deserialize(setting1.DefaultValue.GetType()) ?? setting1.DefaultValue;
                        else setting1.Value = setting.Value;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        
            _afterLoadingActions.ForEach(x => x.Invoke());
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void Save(string path)
    {
        try
        {
            var saveD = Settings.ToDictionary(s => s.Key, s => s.Value.Value);
            using var stream = File.Create(path);
            JsonSerializer.Serialize(stream, saveD, saveD.GetType(), new JsonSerializerOptions(){WriteIndented = true});
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void Reset(string key)
    {
        if (Settings.TryGetValue(key, out var setting))
        {
            setting.Value = setting.DefaultValue;
        }
    }
    
    public void ResetAll()
    {
        foreach (var setting in Settings.Where(x => x.Value is not PathSetting))
        {
            setting.Value.Value = setting.Value.DefaultValue;
        }
    }

    public void WhenLoaded(Action action)
    {
        _afterLoadingActions.Add(action);
    }
}