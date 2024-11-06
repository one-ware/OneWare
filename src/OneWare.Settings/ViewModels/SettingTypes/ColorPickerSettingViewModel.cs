using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class ColorPickerSettingViewModel : TitledSettingViewModel
{
    public ColorPickerSettingViewModel(TitledSetting setting, IObservable<bool>? needEnabled = null) : base(setting, needEnabled)
    {
    }
}