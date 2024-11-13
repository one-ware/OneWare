using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class TextBoxSettingViewModel : TitledSettingViewModel
{
    public TextBoxSettingViewModel(TitledSetting setting) : base(setting)
    {
        if (setting is TextBoxSetting tbs)
        {
            Watermark = tbs.Watermark;
        }
    }

    public string? Watermark { get; }
}