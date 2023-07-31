using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Settings.ViewModels.SettingTypes;

namespace OneWare.Settings.ViewModels;

public class SettingsCollectionViewModel : ObservableObject
{
    public bool ShowTitle { get; set; } = true;
    public IImage? Icon { get; set; }

    public List<SettingViewModel> SettingModels { get; } = new();

    public SettingsCollectionViewModel(string label, string? iconKey = null, string? toolTip = null)
    {
        Header = label;
        SidebarHeader = label.Split(" ")[0];
        IconKey = iconKey;

        if (Application.Current == null) throw new NullReferenceException("Application.Current is null");

        if (iconKey == null) return;
        
        Application.Current.GetResourceObservable(iconKey).Subscribe(x =>
        {
            Icon = x as IImage;
        });
    }

    public string? IconKey { get; }
    public string Header { get; }
    public string SidebarHeader { get; }
}