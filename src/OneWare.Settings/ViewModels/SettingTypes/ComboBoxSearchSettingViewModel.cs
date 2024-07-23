namespace OneWare.Settings.ViewModels.SettingTypes;

public class ComboBoxSearchSettingViewModel : SettingViewModel
{
    public ComboBoxSearchSettingViewModel(ComboBoxSetting setting) : base(setting)
    {
        Setting = setting;
    }

    public new ComboBoxSetting Setting { get; }
}