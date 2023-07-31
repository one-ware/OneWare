using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Settings.ViewModels.SettingTypes;

public abstract class SettingViewModel : ObservableObject
{
    private bool _isEnabled = true;

    public TitledSetting Setting { get; }

    protected SettingViewModel(TitledSetting setting)
    {
        Setting = setting;
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}