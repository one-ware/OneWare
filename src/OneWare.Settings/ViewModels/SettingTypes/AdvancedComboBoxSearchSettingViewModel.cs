using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class AdvancedComboBoxSearchSettingViewModel(AdvancedComboBoxSearchSetting setting) : TitledSettingViewModel(setting)
{
    public AdvancedComboBoxSearchSetting AdvancedSetting { get; } = setting;
}