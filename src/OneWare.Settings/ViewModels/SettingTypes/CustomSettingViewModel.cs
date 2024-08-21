using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class CustomSettingViewModel : SettingViewModel
{
    public CustomSettingViewModel(CustomSetting setting)
    {
        Setting = setting;
    }
    
    public CustomSetting Setting { get; }
}