using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class TextBoxSettingViewModel : TitledSettingViewModel
{
    public TextBoxSettingViewModel(TextBoxSetting setting) : base(setting)
    {
        Watermark = setting.Watermark;
    }

    public TextBoxSettingViewModel(TitledSetting setting) : base(setting)
    {
    }

    public string? Watermark { get; }
}