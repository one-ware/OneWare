namespace OneWare.Settings.ViewModels.SettingTypes;

public class SliderSettingViewModel : SettingViewModel
{
    public SliderSettingViewModel(SliderSetting setting) : base(setting)
    {
        Setting = setting;
    }

    public new SliderSetting Setting { get; }
}