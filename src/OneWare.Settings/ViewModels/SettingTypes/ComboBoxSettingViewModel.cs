using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class ComboBoxSettingViewModel : TitledSettingViewModel
{
    public ComboBoxSettingViewModel(ComboBoxSetting setting, IObservable<bool>? needEnabled = null) : base(setting, needEnabled)
    {
        Setting = setting;
    }

    public new ComboBoxSetting Setting { get; }
}