using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Settings.ViewModels;

public class SettingsPageViewModel : ObservableObject
{
    private string _header;

    private IImage? _icon;
    private bool _isExpanded;

    private ObservableCollection<SettingsCollectionViewModel> _settingCollections = new();

    public SettingsPageViewModel(string title, string? iconKey)
    {
        _header = title;

        if (Application.Current == null) throw new NullReferenceException(nameof(Application.Current));

        if (iconKey == null) return;

        Application.Current.GetResourceObservable(iconKey).Subscribe(x => { Icon = x as IImage; });
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    public IImage? Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    public ObservableCollection<SettingsCollectionViewModel> SettingCollections
    {
        get => _settingCollections;
        set => SetProperty(ref _settingCollections, value);
    }
}