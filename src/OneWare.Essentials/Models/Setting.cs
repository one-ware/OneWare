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
}

public class CheckBoxSetting : TitledSetting
{
    public CheckBoxSetting(string title, bool defaultValue) : base(title, defaultValue)
    {
    }
}

public class TextBoxSetting : TitledSetting
{
    public TextBoxSetting(string title, object defaultValue, string? watermark) : base(title, defaultValue)
    {
        Watermark = watermark;
    }

    public string? Watermark { get; }
}

public class ComboBoxSetting : TitledSetting
{
    public ComboBoxSetting(string title, object defaultValue, IEnumerable<object> options) : base(title, defaultValue)
    {
        Options = options.ToArray();
    }
    
    public object[] Options { get; }
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
}

public class ComboBoxSearchSetting(string title, object defaultValue, IEnumerable<object> options)
    : ComboBoxSetting(title, defaultValue, options);

public class SliderSetting : TitledSetting
{
    public SliderSetting(string title, double defaultValue, double min, double max, double step) : base(
        title, defaultValue)
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

    protected PathSetting(string title, string defaultValue, string? watermark,
        string? startDirectory, Func<string, bool>? checkPath) : base(title, defaultValue, watermark)
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
}

public abstract class CustomSetting : Setting
{
    public CustomSetting(object defaultValue) : base(defaultValue)
    {
        
    }
    
    public object? Control { get; init; }
}