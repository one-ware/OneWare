namespace OneWare.Settings.ViewModels.SettingTypes;

public class ComboBoxSettingViewModel : SettingViewModel
{
    public new ComboBoxSetting Setting { get; }
        
    public ComboBoxSettingViewModel(ComboBoxSetting setting) : base(setting)
    {
        this.Setting = setting;
    }
}