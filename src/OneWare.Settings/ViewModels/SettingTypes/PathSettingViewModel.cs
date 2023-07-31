using Avalonia;
using Avalonia.Controls;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class PathSettingViewModel : TextBoxSettingViewModel
{
    private PathSetting _pathSetting;
    public PathSettingViewModel(PathSetting setting) : base(setting)
    {
        _pathSetting = setting;
    }

    public Task SelectPathAsync(Visual visual)
    {
        var topLevel = TopLevel.GetTopLevel(visual);
        return topLevel != null ? _pathSetting.SelectPathAsync(topLevel) : Task.CompletedTask;
    }
}