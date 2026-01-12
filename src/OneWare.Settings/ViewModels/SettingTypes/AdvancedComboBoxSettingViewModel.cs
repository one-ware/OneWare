using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class AdvancedComboBoxSettingViewModel(AdvancedComboBoxSetting setting) : TitledSettingViewModel(setting)
{
    public AdvancedComboBoxSetting AdvancedSetting { get; } = setting;
}