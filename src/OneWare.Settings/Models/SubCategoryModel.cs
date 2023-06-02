using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace OneWare.Settings.Models;

public class SubCategoryModel
{
    public IImage? Icon { get; set; }

    public List<SettingModel> SettingModels { get; } = new();

    public SubCategoryModel(string label, string? iconKey = null, string? toolTip = null)
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