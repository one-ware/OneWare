namespace OneWare.Settings.ViewModels.SettingTypes;

public class TextBoxSettingViewModel : SettingViewModel
{
    public string? Watermark { get; }
    public TextBoxSettingViewModel(TextBoxSetting setting) : base(setting)
    {
        Watermark = setting.Watermark;
    }
    
    public TextBoxSettingViewModel(TitledSetting setting) : base(setting)
    {
    }
}