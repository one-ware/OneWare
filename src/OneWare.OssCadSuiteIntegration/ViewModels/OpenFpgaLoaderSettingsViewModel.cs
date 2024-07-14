using OneWare.Essentials.Controls;
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
    private readonly TitledSetting _boardSetting;
    private readonly IFpga _fpga;
    private readonly TitledSetting _longTermFlagsSetting;
    private readonly UniversalFpgaProjectRoot _projectRoot;
    private readonly Dictionary<string, string> _settings;
    private readonly TitledSetting _shortTermFlagsSetting;

    public OpenFpgaLoaderSettingsViewModel(UniversalFpgaProjectRoot projectRoot, IFpga fpga)
    {
        _projectRoot = projectRoot;
        _fpga = fpga;

        Title = "OpenFPGALoader Settings";
        Id = "OpenFpgaLoaderSettings";

        var defaultProperties = fpga.Properties;
        _settings = FpgaSettingsParser.LoadSettings(projectRoot, fpga.Name);

        _boardSetting = new TitledSetting("Board", "OpenFPGALoader Board",
            defaultProperties.GetValueOrDefault("OpenFpgaLoader_Board") ?? "");

        _shortTermFlagsSetting = new TitledSetting("Short Term Arguments",
            "OpenFPGALoader Flags for Short Term Programming",
            defaultProperties.GetValueOrDefault("OpenFpgaLoader_ShortTerm_Flags") ?? "");

        _longTermFlagsSetting = new TitledSetting("Long Term Arguments",
            "OpenFPGALoader Flags for Long Term Programming",
            defaultProperties.GetValueOrDefault("OpenFpgaLoader_LongTerm_Flags") ?? "");

        if (_settings.TryGetValue("OpenFpgaLoader_Board", out var oflBoard))
            _boardSetting.Value = oflBoard;

        if (_settings.TryGetValue("OpenFpgaLoader_ShortTerm_Flags", out var oflSFlags))
            _shortTermFlagsSetting.Value = oflSFlags;

        if (_settings.TryGetValue("OpenFpgaLoader_LongTerm_Flags", out var oflLFlags))
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
        _settings["OpenFpgaLoader_Board"] = _boardSetting.Value.ToString()!;
        _settings["OpenFpgaLoader_ShortTerm_Flags"] = _shortTermFlagsSetting.Value.ToString()!;
        _settings["OpenFpgaLoader_LongTerm_Flags"] = _longTermFlagsSetting.Value.ToString()!;

        FpgaSettingsParser.SaveSettings(_projectRoot, _fpga.Name, _settings);

        Close(flexibleWindow);
    }

    public void Reset()
    {
        foreach (var setting in SettingsCollection.SettingModels) setting.Setting.Value = setting.Setting.DefaultValue;
    }
}