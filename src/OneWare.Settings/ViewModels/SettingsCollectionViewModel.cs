using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using OneWare.Essentials.Models;
using OneWare.Settings.ViewModels.SettingTypes;

namespace OneWare.Settings.ViewModels;

public class SettingsCollectionViewModel : ObservableObject, ISearchableSettingsItem
{
    private bool _isExpanded;
    private bool _isVisibleBySearch = true;

    public SettingsCollectionViewModel(string label, string? iconKey = null, string? toolTip = null)
    {
        Header = label;
        SidebarHeader = label.Split(" ")[0];
#pragma warning disable CS0618 // Write to obsolete IconKey property kept for backwards compatibility.
        IconKey = iconKey;
#pragma warning restore CS0618

        SettingModels.CollectionChanged += (sender, args) =>
        {
            if (args.NewItems != null) ConstructViewModels(args.NewItems.Cast<Setting>());
        };

        if (Application.Current == null) throw new NullReferenceException("Application.Current is null");

        // Category icons are no longer rendered in the Settings window, but the iconKey resource
        // lookup is preserved for backwards compatibility with code that may read the Icon property.
        if (iconKey == null) return;

#pragma warning disable CS0618 // Write to obsolete Icon property kept for backwards compatibility.
        Application.Current.GetResourceObservable(iconKey).Subscribe(x => { Icon = x as IImage; });
#pragma warning restore CS0618
    }

    public bool ShowTitle { get; set; } = true;

    [Obsolete("Category icons are no longer displayed in the Application Settings window. This property is retained only for backwards compatibility.")]
    public IImage? Icon { get; set; }

    public ObservableCollection<Setting> SettingModels { get; } = [];

    public ObservableCollection<SettingViewModel> SettingViewModels { get; } = [];

    [Obsolete("Category icons are no longer displayed in the Application Settings window. This property is retained only for backwards compatibility.")]
    public string? IconKey { get; }
    public string Header { get; }
    public string SidebarHeader { get; }

    public bool IsVisibleBySearch
    {
        get => _isVisibleBySearch;
        set => SetProperty(ref _isVisibleBySearch, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    private void ConstructViewModels(IEnumerable<Setting> source)
    {
        foreach (var setting in source)
            switch (setting)
            {
                case ComboBoxSearchSetting csS:
                    SettingViewModels.Add(new ComboBoxSearchSettingViewModel(csS));
                    break;
                case AdvancedComboBoxSearchSetting aCss:
                    SettingViewModels.Add(new AdvancedComboBoxSearchSettingViewModel(aCss));
                    break;
                case AdvancedComboBoxSetting aCs:
                    SettingViewModels.Add(new AdvancedComboBoxSettingViewModel(aCs));
                    break;
                case ComboBoxSetting cS:
                    SettingViewModels.Add(new ComboBoxSettingViewModel(cS));
                    break;
                case SliderSetting ss:
                    SettingViewModels.Add(new SliderSettingViewModel(ss));
                    break;
                case FolderPathSetting pS:
                    SettingViewModels.Add(new PathSettingViewModel(pS));
                    break;
                case FilePathSetting pS:
                    SettingViewModels.Add(new PathSettingViewModel(pS));
                    break;
                case PathSetting pS:
                    SettingViewModels.Add(new PathSettingViewModel(pS));
                    break;
                case ListBoxSetting lS:
                    SettingViewModels.Add(new ListBoxSettingViewModel(lS));
                    break;
                case CheckBoxSetting cbS:
                    SettingViewModels.Add(new CheckBoxSettingViewModel(cbS));
                    break;
                case TextBoxSetting tS:
                    SettingViewModels.Add(new TextBoxSettingViewModel(tS));
                    break;
                case ColorSetting cS:
                    SettingViewModels.Add(new ColorPickerSettingViewModel(cS));
                    break;
                case CustomSetting cS:
                    SettingViewModels.Add(new CustomSettingViewModel(cS));
                    break;
            }

        var sorted = new ObservableCollection<SettingViewModel>(
            SettingViewModels.OrderBy(s => s.Setting.Priority)
        );
        SettingViewModels.Clear();
        SettingViewModels.AddRange(sorted);
    }

    /// <summary>
    ///     Clears all settings and their view models.
    /// </summary>
    public void Clear()
    {
        SettingModels.Clear();
        SettingViewModels.Clear();
    }
}
