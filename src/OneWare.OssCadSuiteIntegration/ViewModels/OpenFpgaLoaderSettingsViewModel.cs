using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;
using OneWare.Settings;
using OneWare.Settings.ViewModels;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;

namespace OneWare.OssCadSuiteIntegration.ViewModels;

public class OpenFpgaLoaderSettingsViewModel : FlexibleWindowViewModelBase
{
    private readonly TextBoxSetting _boardSetting;
    private readonly IFpga _fpga;
    private readonly TextBoxSetting _longTermFlagsSetting;
    private readonly UniversalFpgaProjectRoot _projectRoot;
    private readonly Dictionary<string, string> _settings;
    private readonly TextBoxSetting _shortTermFlagsSetting;

    public OpenFpgaLoaderSettingsViewModel(UniversalFpgaProjectRoot projectRoot, IFpga fpga)
    {
        _projectRoot = projectRoot;
        _fpga = fpga;

        Title = "OpenFPGALoader Settings";
        Id = "OpenFpgaLoaderSettings";

        var defaultProperties = fpga.Properties;
        _settings = FpgaSettingsParser.LoadSettings(projectRoot, fpga.Name);

        _boardSetting = new TextBoxSetting("Board", "OpenFPGALoader Board",
            defaultProperties.GetValueOrDefault("openFpgaLoaderBoard") ?? "", null);

        _shortTermFlagsSetting = new TextBoxSetting("Short Term Arguments",
            "OpenFPGALoader Flags for Short Term Programming",
            defaultProperties.GetValueOrDefault("openFpgaLoaderShortTermFlags") ?? "", null);

        _longTermFlagsSetting = new TextBoxSetting("Long Term Arguments",
            "OpenFPGALoader Flags for Long Term Programming",
            defaultProperties.GetValueOrDefault("openFpgaLoaderLongTermFlags") ?? "", null);

        if (_settings.TryGetValue("openFpgaLoaderBoard", out var oflBoard))
            _boardSetting.Value = oflBoard;

        if (_settings.TryGetValue("openFpgaLoaderShortTermFlags", out var oflSFlags))
            _shortTermFlagsSetting.Value = oflSFlags;

        if (_settings.TryGetValue("openFpgaLoaderLongTermFlags", out var oflLFlags))
            _longTermFlagsSetting.Value = oflLFlags;

        SettingsCollection.SettingModels.Add(new TextBoxSettingViewModel(_boardSetting));
        SettingsCollection.SettingModels.Add(new TextBoxSettingViewModel(_shortTermFlagsSetting));
        SettingsCollection.SettingModels.Add(new TextBoxSettingViewModel(_longTermFlagsSetting));
    }

    public SettingsCollectionViewModel SettingsCollection { get; } = new("OpenFPGALoader Settings")
    {
        ShowTitle = false
    };

    public void Save(FlexibleWindow flexibleWindow)
    {
        _settings["openFpgaLoaderBoard"] = _boardSetting.Value.ToString()!;
        _settings["openFpgaLoaderShortTermFlags"] = _shortTermFlagsSetting.Value.ToString()!;
        _settings["openFpgaLoaderLongTermFlags"] = _longTermFlagsSetting.Value.ToString()!;

        FpgaSettingsParser.SaveSettings(_projectRoot, _fpga.Name, _settings);

        Close(flexibleWindow);
    }

    public void Reset()
    {
        foreach (var setting in SettingsCollection.SettingModels) setting.Setting.Value = setting.Setting.DefaultValue;
    }
}