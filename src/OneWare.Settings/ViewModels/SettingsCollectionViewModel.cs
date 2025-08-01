using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;
using OneWare.Settings.ViewModels.SettingTypes;

namespace OneWare.Settings.ViewModels;

public class SettingsCollectionViewModel : ObservableObject
{
    public SettingsCollectionViewModel(string label, string? iconKey = null, string? toolTip = null)
    {
        Header = label;
        SidebarHeader = label.Split(" ")[0];
        IconKey = iconKey;

        SettingModels.CollectionChanged += (sender, args) =>
        {
            if (args.NewItems != null)
            {
                ConstructViewModels(args.NewItems.Cast<Setting>());
            }
        };
        
        if (Application.Current == null) throw new NullReferenceException("Application.Current is null");

        if (iconKey == null) return;

        Application.Current.GetResourceObservable(iconKey).Subscribe(x => { Icon = x as IImage; });
    }
    
    public bool ShowTitle { get; set; } = true;
    public IImage? Icon { get; set; }

    public ObservableCollection<Setting> SettingModels { get; } = [];

    public ObservableCollection<SettingViewModel> SettingViewModels { get; } = [];

    public string? IconKey { get; }
    public string Header { get; }
    public string SidebarHeader { get; }

    private void ConstructViewModels(IEnumerable<Setting> source)
    {
        foreach (var setting in source)
        {
            switch (setting)
            {
                case ComboBoxSearchSetting csS:
                    SettingViewModels.Add(new ComboBoxSearchSettingViewModel(csS));
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
        }
        
        var sorted = new ObservableCollection<SettingViewModel>(
            SettingViewModels.OrderBy(s => s.Setting.Priority) 
        );
        SettingViewModels.Clear();
        SettingViewModels.AddRange(sorted);

    }
    
    /// <summary>
    /// Clears all settings and their view models.
    /// </summary>
    public void Clear()
    {
        SettingModels.Clear();
        SettingViewModels.Clear();
    }
}