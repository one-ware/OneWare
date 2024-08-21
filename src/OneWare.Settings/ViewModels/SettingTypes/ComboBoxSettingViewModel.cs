using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class ComboBoxSettingViewModel : TitledSettingViewModel
{
    public ComboBoxSettingViewModel(ComboBoxSetting setting) : base(setting)
    {
        Setting = setting;
    }

    public new ComboBoxSetting Setting { get; }
}