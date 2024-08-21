using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class ComboBoxSearchSettingViewModel : TitledSettingViewModel
{
    public ComboBoxSearchSettingViewModel(ComboBoxSetting setting) : base(setting)
    {
        Setting = setting;
    }

    public new ComboBoxSetting Setting { get; }
}