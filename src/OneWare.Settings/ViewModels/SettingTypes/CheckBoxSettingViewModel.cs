namespace OneWare.Settings.ViewModels.SettingTypes;

public class CheckBoxSettingViewModel : SettingViewModel
{
    public CheckBoxSettingViewModel(TitledSetting setting, IObservable<bool>? needEnabled = null) :
        base(setting)
    {
        needEnabled?.Subscribe(x =>
        {
            IsEnabled = x;
            if (!x) setting.Value = false;
        });
    }
}