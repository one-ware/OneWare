using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
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

public class ComboBoxSetting : TitledSetting
{
    public object[] Options { get; }
    public ComboBoxSetting(string title, string description, object defaultValue, IEnumerable<object> options) : base(title, description, defaultValue)
    {
        Options = options.ToArray();
    }
}

public abstract class PathSetting : TitledSetting
{
    public string StartDirectory { get; }
    
    protected PathSetting(string title, string description, object defaultValue, string startDirectory) : base(title, description, defaultValue)
    {
        StartDirectory = startDirectory;
    }

    public abstract Task SelectPathAsync(TopLevel topLevel);
}
public class FolderPathSetting : PathSetting
{
    public FolderPathSetting(string title, string description, object defaultValue, string startDirectory) : base(title, description, defaultValue, startDirectory)
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
    
    public FilePathSetting(string title, string description, object defaultValue, string startDirectory, params FilePickerFileType[] filters) : base(title, description, defaultValue, startDirectory)
    {
        Filters = filters;
    }

    public override async Task SelectPathAsync(TopLevel topLevel)
    {
        var folder = await Tools.SelectFileAsync(topLevel, Title, StartDirectory, Filters);
        if (folder != null) Value = folder;
    }
}