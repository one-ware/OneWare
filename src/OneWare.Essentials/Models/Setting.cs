using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Essentials.Helpers;

namespace OneWare.Essentials.Models;

public class Setting : ObservableObject
{
    private object _value;

    public int Priority { get; set; } = 0;

    public Setting(object defaultValue)
    {
        DefaultValue = defaultValue;
        _value = defaultValue;
    }

    public virtual object Value
    {
        get => _value;
        set
        {
            var originalType = DefaultValue.GetType();
            if(value.GetType() != originalType)
                value = Convert.ChangeType(value, originalType);
            SetProperty(ref _value, value);
        }
    }

    public object DefaultValue { get; }
}

public abstract class TitledSetting : Setting
{
    public TitledSetting(string title, object defaultValue) : base(defaultValue)
    {
        Title = title;
    }
    
    public string Title { get; }
    
    public string? HoverDescription { get; init; }
    
    public string? MarkdownDocumentation { get; init; }
    
    public IObservable<bool>? IsEnabledObservable { get; init; }
    
    public IObservable<bool>? IsVisibleObservable { get; init; }

    public abstract TitledSetting Clone();
}

public class CheckBoxSetting : TitledSetting
{
    public CheckBoxSetting(string title, bool defaultValue) : base(title, defaultValue)
    {
    }

    public override TitledSetting Clone()
    {
        return new CheckBoxSetting(this.Title, (bool)this.DefaultValue);
    }
}

public class TextBoxSetting : TitledSetting
{
    private string? _watermark;
    
    public TextBoxSetting(string title, object defaultValue, string? watermark) : base(title, defaultValue)
    {
        _watermark = watermark;
    }

    public string? Watermark
    {
        get => _watermark;
        set => SetProperty(ref _watermark, value);
    }
    
    public override TitledSetting Clone()
    {
        return new TextBoxSetting(this.Title, this.DefaultValue, Watermark);
    }
}

public class ComboBoxSetting : TitledSetting
{
    private object[] _options;
    
    public ComboBoxSetting(string title, object defaultValue, IEnumerable<object> options) : base(title, defaultValue)
    {
        _options = options.ToArray();
    }

    public object[] Options
    {
        get => _options;
        set => SetProperty(ref _options, value);
    }
    
    public override TitledSetting Clone()
    {
        return new ComboBoxSetting(this.Title, this.DefaultValue, Options);
    }
}

public class ListBoxSetting : TitledSetting
{
    public ListBoxSetting(string title, params string[] defaultValue) : base(title, new ObservableCollection<string>(defaultValue))
    {
    }

    public ObservableCollection<string> Items
    {
        get => (Value as ObservableCollection<string>)!;
        set => Value = value;
    }

    public override TitledSetting Clone()
    {
        return new ListBoxSetting(this.Title, ((ObservableCollection<string>)this.DefaultValue).ToArray());
    }
}

public class ComboBoxSearchSetting(string title, object defaultValue, IEnumerable<object> options)
    : ComboBoxSetting(title, defaultValue, options);

public class SliderSetting : TitledSetting
{
    private double _min;
    
    private double _max;
    
    private double _step;
    
    public SliderSetting(string title, double defaultValue, double min, double max, double step) : base(
        title, defaultValue)
    {
        _min = min;
        _max = max;
        _step = step;
    }

    public double Min
    {
        get => _min;
        set => SetProperty(ref _min, value);
    }

    public double Max
    {
        get => _max;
        set => SetProperty(ref _max, value);
    }

    public double Step
    {
        get => _step;
        set => SetProperty(ref _step, value);
    }
    
    public override TitledSetting Clone()
    {
        return new SliderSetting(this.Title, (double)this.DefaultValue, Min, Max, Step);
    }
}

public abstract class PathSetting : TextBoxSetting
{
    private bool _isValid = true;

    protected PathSetting(string title, string defaultValue, string? watermark,
        string? startDirectory, Func<string, bool>? checkPath) : base(title, defaultValue, watermark)
    {
        StartDirectory = startDirectory;
        CheckPath = checkPath;

        if (checkPath != null)
        {
            CanVerify = true;
            this.WhenValueChanged(x => x.Value).Subscribe(x =>
            {
                IsValid = checkPath.Invoke((x as string)!);
            });
        }
    }

    public string? StartDirectory { get; }
    public bool CanVerify { get; }
    
    public Func<string, bool>? CheckPath { get; }

    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    public abstract Task SelectPathAsync(TopLevel topLevel);
}

public class FolderPathSetting : PathSetting
{
    public FolderPathSetting(string title, string defaultValue, string? watermark,
        string? startDirectory, Func<string, bool>? checkPath)
        : base(title, defaultValue, watermark, startDirectory, checkPath)
    {
    }

    public override async Task SelectPathAsync(TopLevel topLevel)
    {
        var folder = await StorageProviderHelper.SelectFolderAsync(topLevel, Title, StartDirectory);
        if (folder != null) Value = folder;
    }
}

public class FilePathSetting : PathSetting
{
    public FilePathSetting(string title, string defaultValue, string? watermark,
        string? startDirectory, Func<string, bool>? checkPath, params FilePickerFileType[] filters)
        : base(title, defaultValue, watermark, startDirectory, checkPath)
    {
        Filters = filters;
    }

    private FilePickerFileType[] Filters { get; }

    public override async Task SelectPathAsync(TopLevel topLevel)
    {
        var folder = await StorageProviderHelper.SelectFileAsync(topLevel, Title, StartDirectory, Filters);
        if (folder != null) Value = folder;
    }
}

public class ColorSetting : TitledSetting
{
    public ColorSetting(string title, Color defaultValue) : base(title, defaultValue)
    {
        
    }

    public override TitledSetting Clone()
    {
        return new ColorSetting(this.Title, (Color)this.DefaultValue);
    }
}

public class ProjectSetting(
    string key,
    TitledSetting setting,
    Func<IProjectRootWithFile, bool> activationFunction,
    string? category = null,
    int displayOrder = 0)
{
    public string Category { get; } = category ?? "General";

    public string Key { get; } = key;
    public TitledSetting Setting { get; } = setting;
    public Func<IProjectRootWithFile, bool> ActivationFunction { get; } = activationFunction;
}


public class CategorySetting(string key, string name)
{
    public string Key { get; } = key;
    public string Name { get; } = name;
}

public abstract class CustomSetting : Setting
{
    public object? Control { get; init; }
    public CustomSetting(object defaultValue) : base(defaultValue)
    {
        
    }
}