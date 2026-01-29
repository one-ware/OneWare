using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class CustomSettingViewModel : SettingViewModel
{
    public CustomSettingViewModel(CustomSetting setting)
    {
        Setting = setting;
    }

    public override CustomSetting Setting { get; }
}