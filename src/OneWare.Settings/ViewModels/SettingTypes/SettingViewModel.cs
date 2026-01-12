using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public abstract class SettingViewModel : ObservableObject
{
    public abstract CollectionSetting Setting { get; }

    public ICommand RestoreDefaultCommand => new RelayCommand(() =>
    {
        Setting.Value = Setting.DefaultValue;
    });
}