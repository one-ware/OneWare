using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public abstract class TitledSettingViewModel : SettingViewModel
{
    protected TitledSettingViewModel(TitledSetting setting)
    {
        Setting = setting;
    }

    public override TitledSetting Setting { get; }
}