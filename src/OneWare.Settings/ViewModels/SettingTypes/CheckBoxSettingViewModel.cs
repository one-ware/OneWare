using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class CheckBoxSettingViewModel : TitledSettingViewModel
{
    public CheckBoxSettingViewModel(TitledSetting setting, IObservable<bool>? needEnabled = null) :
        base(setting, needEnabled)
    {
     
    }
}