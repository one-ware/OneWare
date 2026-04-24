using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public abstract class SettingViewModel : ObservableObject
{
    private bool _isVisibleBySearch = true;

    public abstract CollectionSetting Setting { get; }

    public ICommand RestoreDefaultCommand => new RelayCommand(() => { Setting.Value = Setting.DefaultValue; });

    /// <summary>
    ///     Indicates whether this setting matches the current search query in the Application Settings window.
    ///     Combined with <see cref="CollectionSetting.IsVisibleObservable" /> to determine final visibility.
    /// </summary>
    public bool IsVisibleBySearch
    {
        get => _isVisibleBySearch;
        set => SetProperty(ref _isVisibleBySearch, value);
    }
}
