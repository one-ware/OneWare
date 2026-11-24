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

    public SettingsCollectionViewModel(string label, string? toolTip = null)
    {
        Header = label;
        SidebarHeader = label.Split(" ")[0];

        SettingModels.CollectionChanged += (sender, args) =>
        {
            if (args.NewItems != null) ConstructViewModels(args.NewItems.Cast<Setting>());
        };
    }

    public bool ShowTitle { get; set; } = true;

    public ObservableCollection<Setting> SettingModels { get; } = [];

    public ObservableCollection<SettingViewModel> SettingViewModels { get; } = [];

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
                case ComboListBoxSetting clS:
                    SettingViewModels.Add(new ComboListBoxSettingViewModel(clS));
                    break;
                case ToggleSwitchSetting tsS:
                    SettingViewModels.Add(new ToggleSwitchSettingViewModel(tsS));
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
