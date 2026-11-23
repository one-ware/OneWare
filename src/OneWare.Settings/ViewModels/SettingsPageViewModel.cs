using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Settings.ViewModels;

public class SettingsPageViewModel : ObservableObject, ISearchableSettingsItem
{
    private string _header;

    private bool _isExpanded;

    private bool _isVisibleBySearch = true;

    private ObservableCollection<SettingsCollectionViewModel> _settingCollections = new();

    public SettingsPageViewModel(string title)
    {
        _header = title;
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

    public ObservableCollection<SettingsCollectionViewModel> SettingCollections
    {
        get => _settingCollections;
        set => SetProperty(ref _settingCollections, value);
    }

    public bool IsVisibleBySearch
    {
        get => _isVisibleBySearch;
        set => SetProperty(ref _isVisibleBySearch, value);
    }
}
