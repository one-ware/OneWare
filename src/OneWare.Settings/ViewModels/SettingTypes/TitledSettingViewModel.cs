using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public abstract class TitledSettingViewModel : SettingViewModel
{
    private bool _isEnabled = true;

    protected TitledSettingViewModel(TitledSetting setting, IObservable<bool>? needEnabled)
    {
        Setting = setting;
        
        needEnabled?.Subscribe(x =>
        {
            IsEnabled = x;
            if (!x) setting.Value = false;
        });
    }

    public override TitledSetting Setting { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}