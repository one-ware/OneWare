using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public abstract class TitledSettingViewModel : SettingViewModel
{
    private bool _isEnabled = true;

    protected TitledSettingViewModel(TitledSetting setting)
    {
        Setting = setting;
    }

    public override TitledSetting Setting { get; }
}