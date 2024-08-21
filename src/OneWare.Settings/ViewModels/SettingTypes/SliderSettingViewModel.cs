using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class SliderSettingViewModel : TitledSettingViewModel
{
    public SliderSettingViewModel(SliderSetting setting) : base(setting)
    {
        Setting = setting;
    }

    public new SliderSetting Setting { get; }
}