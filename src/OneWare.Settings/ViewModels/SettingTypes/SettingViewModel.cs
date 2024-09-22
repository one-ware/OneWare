using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public abstract class SettingViewModel : ObservableObject
{
    public abstract Setting Setting { get; }
}