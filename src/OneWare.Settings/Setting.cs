using CommunityToolkit.Mvvm.ComponentModel;

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