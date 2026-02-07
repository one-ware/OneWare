using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
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

    public int Priority { get; set; } = 0;

    public virtual object Value
    {
        get => _value;
        set
        {
            var originalType = DefaultValue.GetType();
            if (value.GetType() != originalType)
                value = Convert.ChangeType(value, originalType);
            SetValue(value);
        }
    }

    public object DefaultValue { get; }

    protected virtual void SetValue(object value)
    {
        SetProperty(ref _value, value, nameof(Value));
    }
}

public abstract class CollectionSetting : Setting
{
    protected CollectionSetting(object defaultValue) : base(defaultValue)
    {
    }

    public IObservable<bool>? IsEnabledObservable { get; init; }

    public IObservable<bool>? IsVisibleObservable { get; init; }
}

public abstract class TitledSetting : CollectionSetting
{
    protected const string ValidationFallbackMessage = "Invalid: Please check the value";

    public TitledSetting(string title, object defaultValue) : base(defaultValue)
    {
        Title = title;
    }

    public string Title { get; }

    public string? HoverDescription { get; init; }

    public string? MarkdownDocumentation { get; init; }

    public ISettingValidation? Validator { get; init; }

    public string? ValidationMessage
    {
        get;
        set => SetProperty(ref field, value);
    }

    public abstract TitledSetting Clone();

    protected override void SetValue(object value)
    {
        if (Validator is null)
        {
            base.SetValue(value);
            return;
        }

        try
        {
            ValidationMessage = !Validator.Validate(value, out var validationMsg)
                ? validationMsg ?? ValidationFallbackMessage
                : null;
        }
        catch (ValidationException ex)
        {
            ValidationMessage = string.IsNullOrEmpty(ex.Message) ? ValidationFallbackMessage : ex.Message;
        }
        catch
        {
            ValidationMessage = ValidationFallbackMessage;
        }

        base.SetValue(value);
    }
}

public interface ISettingValidation
{
    bool Validate(object? value, out string? warningMessage);
}

public class CheckBoxSetting : TitledSetting
{
    public CheckBoxSetting(string title, bool defaultValue) : base(title, defaultValue)
    {
    }

    public override TitledSetting Clone()
    {
        return new CheckBoxSetting(Title, (bool)DefaultValue)
        {
	        Validator = Validator
        };
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
        return new TextBoxSetting(Title, DefaultValue, Watermark)
        {
	        Validator = Validator
        };
    }
}

public class ComboBoxSetting : TitledSetting
{
    private object[] _options;

    [Obsolete("Use alternative constructor with object[] constructor instead")]
    public ComboBoxSetting(string title, object defaultValue, IEnumerable<object> options) : base(title, defaultValue)
    {
        _options = options.ToArray();
    }

    public ComboBoxSetting(string title, object defaultValue, object[] options) : base(title, defaultValue)
    {
        _options = options;
    }

    public object[] Options
    {
        get => _options;
        set => SetProperty(ref _options, value);
    }

    public override TitledSetting Clone()
    {
        return new ComboBoxSetting(Title, DefaultValue, Options)
        {
	        Validator = Validator
        };
    }
}

public class AdvancedComboBoxSearchSetting(string title, object defaultValue, AdvancedComboBoxOption[] options)
    : AdvancedComboBoxSetting(title, defaultValue, options)
{
    public override TitledSetting Clone()
    {
        return new AdvancedComboBoxSearchSetting(Title, DefaultValue, Options)
        {
	        Validator = Validator
        };
    }
}

public class AdvancedComboBoxSetting : TitledSetting
{
    public AdvancedComboBoxSetting(string title, object defaultValue, AdvancedComboBoxOption[] options) : base(title,
        defaultValue)
    {
        Options = options;

        this.WhenValueChanged(x => x.Value).Subscribe(x => { OnPropertyChanged(nameof(SelectedItem)); });
    }

    public AdvancedComboBoxOption[] Options
    {
        get;
        set => SetProperty(ref field, value);
    }

    public AdvancedComboBoxOption SelectedItem
    {
        get => Options.FirstOrDefault(x => x.Value.Equals(Value))!;
        set
        {
            if (value?.Value != Value && value != null) Value = value.Value;
        }
    }

    public override TitledSetting Clone()
    {
        return new AdvancedComboBoxSetting(Title, DefaultValue, Options)
        {
	        Validator = Validator
        };
    }
}

public class AdvancedComboBoxOption
{
    public required string Title { get; set; }

    public required object Value { get; set; }

    public string? HoverDescription { get; init; }

    public string? MarkdownDocumentation { get; init; }

    public IObservable<bool>? IsEnabledObservable { get; init; }

    public IObservable<bool>? IsVisibleObservable { get; init; }

    public override string? ToString()
    {
        return Title;
    }
}

public class ListBoxSetting : TitledSetting
{
    public ListBoxSetting(string title, params string[] defaultValue) : base(title,
        new ObservableCollection<string>(defaultValue))
    {
    }

    public ObservableCollection<string> Items
    {
        get => (Value as ObservableCollection<string>)!;
        set => Value = value;
    }

    public override TitledSetting Clone()
    {
        return new ListBoxSetting(Title, ((ObservableCollection<string>)DefaultValue).ToArray())
        {
	        Validator = Validator
        };
    }
}

public class ComboBoxSearchSetting(string title, object defaultValue, IEnumerable<object> options)
    : ComboBoxSetting(title, defaultValue, options.ToArray());

public class SliderSetting : TitledSetting
{
    private double _max;
    private double _min;

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
        return new SliderSetting(Title, (double)DefaultValue, Min, Max, Step)
        {
	        Validator = Validator
        };
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
            this.WhenValueChanged(x => x.Value).Subscribe(x => { IsValid = checkPath.Invoke((x as string)!); });
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
        return new ColorSetting(Title, (Color)DefaultValue)
        {
	        Validator = Validator
        };
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

public abstract class CustomSetting : CollectionSetting
{
    public CustomSetting(object defaultValue) : base(defaultValue)
    {
    }

    public object? Control { get; init; }
}