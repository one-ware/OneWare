namespace OneWare.Settings.ViewModels.SettingTypes;

public class SliderSettingViewModel : SettingViewModel
{
    public new SliderSetting Setting { get; }
    
    public SliderSettingViewModel(SliderSetting setting) : base(setting)
    {
        Setting = setting;
    }
}