using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Settings.ViewModels;

public class SettingsPageViewModel : ObservableObject, ISearchableSettingsItem
{
    private string _header;

    private IImage? _icon;
    private bool _isExpanded;

    private bool _isVisibleBySearch = true;

    private ObservableCollection<SettingsCollectionViewModel> _settingCollections = new();

    public SettingsPageViewModel(string title, string? iconKey)
    {
        _header = title;

        if (Application.Current == null) throw new NullReferenceException(nameof(Application.Current));

        // Category icons are no longer displayed in the Settings window, but the iconKey argument
        // is still resolved so that callers subscribing to the Icon property (external tooling)
        // continue to work. The rendering has been removed on purpose (see issue: Cleanup IDE Settings).
        if (iconKey == null) return;

#pragma warning disable CS0618 // Write to obsolete Icon property kept for backwards compatibility.
        Application.Current.GetResourceObservable(iconKey).Subscribe(x => { Icon = x as IImage; });
#pragma warning restore CS0618
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

    [Obsolete("Category icons are no longer displayed in the Application Settings window. This property is retained only for backwards compatibility.")]
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

    public bool IsVisibleBySearch
    {
        get => _isVisibleBySearch;
        set => SetProperty(ref _isVisibleBySearch, value);
    }
}
