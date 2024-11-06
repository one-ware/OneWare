using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class TextBoxSettingViewModel : TitledSettingViewModel
{
    public TextBoxSettingViewModel(TitledSetting setting, IObservable<bool>? needEnabled = null) : base(setting, needEnabled)
    {
        if (setting is TextBoxSetting tbs)
        {
            Watermark = tbs.Watermark;
        }
    }

    public string? Watermark { get; }
}