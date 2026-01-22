using System.Reactive.Linq;
using System.Text.Json;
using Avalonia.Platform.Storage;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Settings;

public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true
    };
    private readonly List<Action> _afterLoadingActions = [];
    private readonly Dictionary<string, Setting> _settings = new();
    private readonly Dictionary<string, object> _unregisteredSettings = new();
    private Dictionary<string, object>? _loadedSettings;

    public Dictionary<string, SettingCategory> SettingCategories { get; } = new();
    public event EventHandler<SavingEventArgs>? OnSaving;
    public event EventHandler? OnSaved;
    
    public void RegisterSettingCategory(string category, int priority = 0, string? iconKey = null)
    {
        SettingCategories.TryAdd(category, new SettingCategory());
        SettingCategories[category].IconKey = iconKey;
        SettingCategories[category].Priority = priority;
    }

    public void RegisterSettingSubCategory(string category, string subCategory, int priority = 0,
        string? iconKey = null)
    {
        SettingCategories.TryAdd(category, new SettingCategory());
        SettingCategories[category].SettingSubCategories.TryAdd(subCategory, new SettingSubCategory());
        SettingCategories[category].SettingSubCategories[subCategory].Priority = priority;
        SettingCategories[category].SettingSubCategories[subCategory].IconKey = iconKey;
    }

    public void Register<T>(string key, T defaultValue)
    {
        if (defaultValue == null) throw new NullReferenceException(nameof(defaultValue));
        AddSetting(key, new Setting(defaultValue));
    }

    public void RegisterSetting(string category, string subCategory, string key, TitledSetting setting)
    {
        AddSetting(category, subCategory, key, setting);
    }

    public void UpdateSetting(string key, TitledSetting setting)
    {
        throw new NotImplementedException();
    }

    public IObservable<T> Bind<T>(string key, IObservable<T> observable)
    {
        if (!_settings.TryGetValue(key, out var setting))
            throw new ArgumentException($"Setting {key} is not registered!");
        ;
        observable.Skip(1).Subscribe(x => setting.Value = x!);
        return GetSettingObservable<T>(key);
    }

    public void RegisterTitled<T>(string category, string subCategory, string key, string title, string description,
        T defaultValue)
    {
        if (defaultValue == null) throw new NullReferenceException(nameof(defaultValue));
        switch (defaultValue)
        {
            case bool b:
                AddSetting(category, subCategory, key, new CheckBoxSetting(title, b)
                {
                    HoverDescription = description
                });
                break;
            default:
                AddSetting(category, subCategory, key, new TextBoxSetting(title, defaultValue, null)
                {
                    HoverDescription = description
                });
                break;
        }
    }

    public void RegisterTitledPath(string category, string subCategory, string key, string title, string description,
        string defaultValue, string? watermark, string? startDir, Func<string, bool>? validate)
    {
        AddSetting(category, subCategory, key,
            new FolderPathSetting(title, defaultValue, watermark, startDir, validate)
            {
                HoverDescription = description
            });
    }

    public void RegisterTitledFolderPath(string category, string subCategory, string key, string title,
        string description,
        string defaultValue, string? watermark, string? startDir, Func<string, bool>? validate)
    {
        AddSetting(category, subCategory, key,
            new FolderPathSetting(title, defaultValue, watermark, startDir, validate)
            {
                HoverDescription = description
            });
    }

    public void RegisterTitledFilePath(string category, string subCategory, string key, string title,
        string description,
        string defaultValue, string? watermark, string? startDir, Func<string, bool>? validate,
        params FilePickerFileType[] filters)
    {
        AddSetting(category, subCategory, key,
            new FilePathSetting(title, defaultValue, watermark, startDir, validate, filters)
            {
                HoverDescription = description
            });
    }

    public void RegisterTitledSlider(string category, string subCategory, string key, string title,
        string description, double defaultValue, double min, double max, double step)
    {
        AddSetting(category, subCategory, key, new SliderSetting(title, defaultValue, min, max, step)
        {
            HoverDescription = description
        });
    }

    public void RegisterTitledCombo<T>(string category, string subCategory, string key, string title,
        string description, T defaultValue, params T[] options)
    {
        if (defaultValue == null) throw new NullReferenceException(nameof(defaultValue));
        AddSetting(category, subCategory, key,
            new ComboBoxSetting(title, defaultValue, options.Cast<object>())
            {
                HoverDescription = description
            });
    }

    public void RegisterTitledComboSearch<T>(string category, string subCategory, string key, string title,
        string description, T defaultValue, params T[] options)
    {
        if (defaultValue == null) throw new NullReferenceException(nameof(defaultValue));
        AddSetting(category, subCategory, key,
            new ComboBoxSearchSetting(title, defaultValue, options.Cast<object>())
            {
                HoverDescription = description
            });
    }

    public void RegisterTitledListBox(string category, string subCategory, string key, string title,
        string description, params string[] defaultValue)
    {
        if (defaultValue == null) throw new NullReferenceException(nameof(defaultValue));
        AddSetting(category, subCategory, key,
            new ListBoxSetting(title, defaultValue)
            {
                HoverDescription = description
            });
    }

    public void RegisterCustom(string category, string subCategory, string key, CustomSetting customSetting)
    {
        AddSetting(category, subCategory, key, customSetting);
    }

    public Setting GetSetting(string key)
    {
        _settings.TryGetValue(key, out var value);
        return value ?? throw new ArgumentException($"Setting {key} is not registered!");
    }

    public bool HasSetting(string key)
    {
        return _settings.ContainsKey(key);
    }

    public T GetSettingValue<T>(string key)
    {
        if (!_settings.TryGetValue(key, out var setting))
        {
            throw new ArgumentException($"Setting with key '{key}' is not registered.", nameof(key));
        }
        
        if (setting?.Value is null)
        {
            throw new NullReferenceException($"Setting '{key}' exists but has no value.");
        }
        
        try
        {
            return (T)Convert.ChangeType(setting.Value, typeof(T));
        }
        catch (InvalidCastException ex)
        {
            throw new InvalidCastException(
                $"Setting '{key}' cannot be converted from type '{setting.Value.GetType().Name}' to '{typeof(T).Name}'.",
                ex);
        }
        catch (FormatException ex)
        {
            throw new FormatException(
                $"Setting '{key}' with value '{setting.Value}' cannot be parsed as '{typeof(T).Name}'.",
                ex);
        }
    }

    public T[] GetComboOptions<T>(string key)
    {
        _settings.TryGetValue(key, out var value);
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
        _settings.TryGetValue(key, out var s);
        if (s == null) throw new Exception($"Error setting Setting: {key} does not exist!");
        s.Value = value;
    }

    public IObservable<T> GetSettingObservable<T>(string key)
    {
        _settings.TryGetValue(key, out var value);
        if (value != null) return value.WhenValueChanged(x => x.Value)!.Cast<T>();
        throw new ArgumentException($"Setting {key} is not registered!");
    }

    public void Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var stream = File.OpenRead(path);
            _loadedSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(stream, JsonSerializerOptions);
            if (_loadedSettings == null) return;
            foreach (var (key, setting) in _loadedSettings)
                try
                {
                    if (_settings.TryGetValue(key, out var registeredSetting))
                    {
                        if (setting is JsonElement je)
                            registeredSetting.Value = je.Deserialize(registeredSetting.DefaultValue.GetType()) ??
                                                      registeredSetting.DefaultValue;
                        else registeredSetting.Value = setting;

                        _unregisteredSettings.Remove(key);
                    }
                    else
                    {
                        _unregisteredSettings.TryAdd(key, setting);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
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
            var saveD = _settings.ToDictionary(s => s.Key, s => s.Value.Value);

            foreach (var unregistered in _unregisteredSettings)
            {
                saveD.TryAdd(unregistered.Key, unregistered.Value);
            }

            if (_loadedSettings != null)
                foreach (var (key, value) in _loadedSettings)
                    saveD.TryAdd(key, value);

            using var stream = File.Create(path);
            JsonSerializer.Serialize(stream, saveD, saveD.GetType(), JsonSerializerOptions);
            OnSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void Reset(string key)
    {
        if (_settings.TryGetValue(key, out var setting)) setting.Value = setting.DefaultValue;
    }

    public void ResetAll()
    {
        foreach (var setting in _settings.Where(x => x.Value is not PathSetting))
            setting.Value.Value = setting.Value.DefaultValue;
        _unregisteredSettings.Clear();
    }

    public void WhenLoaded(Action action)
    {
        _afterLoadingActions.Add(action);
    }

    public void Saving(object host, out bool cancel)
    {
        SavingEventArgs args = new(host);
        OnSaving?.Invoke(this, args);
        cancel = args.Cancelled;
    }

    private void AddSetting(string category, string subCategory, string key, Setting setting)
    {
        AddSetting(key, setting);
        SettingCategories.TryAdd(category, new SettingCategory());
        var cat = SettingCategories[category];
        cat.SettingSubCategories.TryAdd(subCategory, new SettingSubCategory());
        var sub = cat.SettingSubCategories[subCategory];
        sub.Settings.Add(setting);
    }

    private void AddSetting(string key, Setting setting)
    {
        _settings.Add(key, setting);

        try
        {
            if (_unregisteredSettings.TryGetValue(key, out var unregistered))
            {
                if (unregistered is JsonElement je)
                    setting.Value = je.Deserialize(setting.DefaultValue.GetType()) ?? setting.DefaultValue;
                else setting.Value = setting;

                _unregisteredSettings.Remove(key);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}