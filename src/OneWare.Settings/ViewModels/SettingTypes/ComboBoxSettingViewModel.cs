namespace OneWare.Settings.ViewModels.SettingTypes;

public class ComboBoxSettingViewModel : SettingViewModel
{
    public ComboBoxSettingViewModel(ComboBoxSetting setting) : base(setting)
    {
        Setting = setting;
    }

    public new ComboBoxSetting Setting { get; }
}