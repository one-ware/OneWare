using DynamicData.Binding;
using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class SliderSettingViewModel : TitledSettingViewModel
{
    public SliderSettingViewModel(SliderSetting setting) : base(setting)
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
        get => Math.Round((double)Setting.Value, GetPrecision(Setting.Step));
        set
        {
            if (value < Setting.Min || value > Setting.Max)
                throw new ArgumentException();
            
            Setting.Value = Math.Round(value, GetPrecision(Setting.Step));
        }
    }

    // Helper method: how many decimals do we need?
    private int GetPrecision(double step)
    {
        if (step >= 1)
            return 0;

        int precision = 0;
        while (step < 1)
        {
            step *= 10;
            precision++;
        }
        return precision;
    }


}