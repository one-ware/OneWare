using DynamicData.Binding;
using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class SliderSettingViewModel : TitledSettingViewModel
{
    public SliderSettingViewModel(SliderSetting setting, IObservable<bool>? needEnabled = null) : base(setting)
    {
        Setting = setting;
        
        setting.WhenValueChanged(x => x.Value).Subscribe(x =>
        {
            OnPropertyChanged(nameof(TextBoxValue));
        });
    }

    public new SliderSetting Setting { get; }
    
    public double TextBoxValue
    {
        get => (double) Setting.Value;
        set => Setting.Value = value < Setting.Min ? Setting.Min : value > Setting.Max ? Setting.Max : value;
    }
}