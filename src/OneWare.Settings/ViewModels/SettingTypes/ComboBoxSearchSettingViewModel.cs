using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class ComboBoxSearchSettingViewModel : TitledSettingViewModel
{
    public ComboBoxSearchSettingViewModel(ComboBoxSetting setting, IObservable<bool>? needEnabled = null) : base(setting, needEnabled)
    {
        Setting = setting;
    }

    public new ComboBoxSetting Setting { get; }
}