using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Essentials.Helpers;

namespace OneWare.Essentials.Models;

public class Setting : ObservableObject
{
    private object _value;

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

public class TitledSetting : Setting
{
    public TitledSetting(string title, string description, object defaultValue) : base(defaultValue)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }

    public string Description { get; }
}

public class TextBoxSetting : TitledSetting
{
    public TextBoxSetting(string title, string description, object defaultValue, string? watermark) : base(title,
        description, defaultValue)
    {
        Watermark = watermark;
    }

    public string? Watermark { get; }
}

public class ComboBoxSetting : TitledSetting
{
    public ComboBoxSetting(string title, string description, object defaultValue, IEnumerable<object> options) : base(
        title, description, defaultValue)
    {
        Options = options.ToArray();
    }
    
    public object[] Options { get; }
}

public class ListBoxSetting : TitledSetting
{
    public ListBoxSetting(string title, string description, params string[] defaultValue) : base(
        title, description, new ObservableCollection<string>(defaultValue))
    {
    }

    public ObservableCollection<string> Items
    {
        get => (Value as ObservableCollection<string>)!;
        set => Value = value;
    }
}

public class ComboBoxSearchSetting(string title, string description, object defaultValue, IEnumerable<object> options)
    : ComboBoxSetting(title, description, defaultValue, options);

public class SliderSetting : TitledSetting
{
    public SliderSetting(string title, string description, double defaultValue, double min, double max, double step) : base(
        title, description, defaultValue)
    {
        Min = min;
        Max = max;
        Step = step;
    }

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }
}

public abstract class PathSetting : TextBoxSetting
{
    private bool _isValid = true;

    protected PathSetting(string title, string description, object defaultValue, string? watermark,
        string? startDirectory, Func<string, bool>? checkPath) : base(title, description, defaultValue, watermark)
    {
        StartDirectory = startDirectory;

        if (checkPath != null)
        {
            CanVerify = true;
            this.WhenValueChanged(x => x.Value).Subscribe(x => { IsValid = checkPath.Invoke((x as string)!); });
        }
    }

    public string? StartDirectory { get; }
    public bool CanVerify { get; }

    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    public abstract Task SelectPathAsync(TopLevel topLevel);
}

public class FolderPathSetting : PathSetting
{
    public FolderPathSetting(string title, string description, object defaultValue, string? watermark,
        string? startDirectory, Func<string, bool>? checkPath)
        : base(title, description, defaultValue, watermark, startDirectory, checkPath)
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
    public FilePathSetting(string title, string description, object defaultValue, string? watermark,
        string? startDirectory, Func<string, bool>? checkPath, params FilePickerFileType[] filters)
        : base(title, description, defaultValue, watermark, startDirectory, checkPath)
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

public abstract class CustomSetting : Setting
{
    public CustomSetting(object defaultValue) : base(defaultValue)
    {
        
    }
    
    public object? Control { get; init; }
}