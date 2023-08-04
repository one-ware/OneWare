using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Shared;

namespace OneWare.Settings;

public class Setting : ObservableObject
{
    private object _value;

    public object Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
    
    public object DefaultValue { get; }

    public Setting(object defaultValue)
    {
        DefaultValue = defaultValue;
        _value = defaultValue;
    }
}

public class TitledSetting : Setting
{
    public string Title { get; }
    
    public string Description { get; }

    public TitledSetting(string title, string description, object defaultValue) : base(defaultValue)
    {
        Title = title;
        Description = description;
    }
}

public class TextBoxSetting : TitledSetting
{
    public string? Watermark { get; }
    public TextBoxSetting(string title, string description, object defaultValue, string? watermark) : base(title, description, defaultValue)
    {
        Watermark = watermark;
    }
}

public class ComboBoxSetting : TitledSetting
{
    public object[] Options { get; }
    public ComboBoxSetting(string title, string description, object defaultValue, IEnumerable<object> options) : base(title, description, defaultValue)
    {
        Options = options.ToArray();
    }
}

public abstract class PathSetting : TextBoxSetting
{
    public string StartDirectory { get; }
    public bool CanVerify { get; }
    
    private bool _isValid = true;

    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    protected PathSetting(string title, string description, object defaultValue, string? watermark, string startDirectory, Func<string, bool>? checkPath) : base(title, description, defaultValue, watermark)
    {
        StartDirectory = startDirectory;

        if (checkPath != null)
        {
            CanVerify = true;
            this.WhenValueChanged(x => x.Value).Subscribe(x =>
            {
                IsValid = checkPath.Invoke((x as string)!);
            });
        }
    }

    public abstract Task SelectPathAsync(TopLevel topLevel);
}
public class FolderPathSetting : PathSetting
{
    public FolderPathSetting(string title, string description, object defaultValue, string? watermark, string startDirectory, Func<string, bool>? checkPath) 
        : base(title, description, defaultValue, watermark, startDirectory, checkPath)
    {
    }

    public override async Task SelectPathAsync(TopLevel topLevel)
    {
        var folder = await Tools.SelectFolderAsync(topLevel, Title, StartDirectory);
        if (folder != null) Value = folder;
    }
}

public class FilePathSetting : PathSetting
{
    public FilePickerFileType[] Filters { get; }
    
    public FilePathSetting(string title, string description, object defaultValue, string? watermark, string startDirectory, Func<string, bool>? checkPath, params FilePickerFileType[] filters) 
        : base(title, description, defaultValue, watermark, startDirectory, checkPath)
    {
        Filters = filters;
    }

    public override async Task SelectPathAsync(TopLevel topLevel)
    {
        var folder = await Tools.SelectFileAsync(topLevel, Title, StartDirectory, Filters);
        if (folder != null) Value = folder;
    }
}