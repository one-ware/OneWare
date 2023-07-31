using Avalonia;
using Avalonia.Controls;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class PathSettingViewModel : TextBoxSettingViewModel
{
    public PathSetting PathSetting { get; }
    
    public PathSettingViewModel(PathSetting setting) : base(setting)
    {
        PathSetting = setting;
    }

    public Task SelectPathAsync(Visual visual)
    {
        var topLevel = TopLevel.GetTopLevel(visual);
        return topLevel != null ? PathSetting.SelectPathAsync(topLevel) : Task.CompletedTask;
    }
}