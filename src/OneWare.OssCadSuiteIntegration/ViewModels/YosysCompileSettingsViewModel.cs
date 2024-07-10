using OneWare.Essentials.Controls;
using OneWare.Essentials.ViewModels;
using OneWare.Settings;
using OneWare.Settings.ViewModels;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.OssCadSuiteIntegration.ViewModels;

public class YosysCompileSettingsViewModel : FlexibleWindowViewModelBase
{
    private readonly UniversalFpgaProjectRoot _fpgaProjectRoot;
    
    private readonly ComboBoxSetting _yosysSynthToolSetting;
    private readonly TitledSetting _yosysFlagSetting;
    private readonly ComboBoxSetting _nextPnrToolSetting;
    private readonly TitledSetting _nextPnrFlagSetting;
    private readonly ComboBoxSetting _packToolSetting;
    private readonly TitledSetting _packToolFlagSetting;
    private readonly IFpga _selectedFpga;
    private readonly Dictionary<string, string> _settings;
    
    public SettingsCollectionViewModel SettingsCollection { get; } = new("Yosys Settings")
    {
        ShowTitle = false
    };
    
    public YosysCompileSettingsViewModel(UniversalFpgaProjectCompileViewModel compileViewModel, UniversalFpgaProjectRoot fpgaProjectRoot)
    {
        _fpgaProjectRoot = fpgaProjectRoot;
        _selectedFpga = compileViewModel.SelectedFpgaModel?.Fpga ?? throw new NullReferenceException(nameof(compileViewModel.SelectedFpgaModel));

        Title = "Yosys Compile Settings";
        Id = "YosysCompileSettings";

        var defaultProperties = _selectedFpga.Properties;
        _settings = FpgaSettingsParser.LoadSettings(fpgaProjectRoot, _selectedFpga.Name);
        
        //Add missing default properties
        foreach (var property in defaultProperties)
        {
            _settings.TryAdd(property.Key, property.Value);
        }

        _yosysSynthToolSetting = new ComboBoxSetting("Yosys Synth Tool", "Set Yosys Synth tool", defaultProperties.GetValueOrDefault("YosysToolchain_Yosys_SynthTool") ?? "", [
            "synth_achronix",
            "synth_anlogic",
            "synth_coolrunner2",
            "synth_easic",
            "synth_ecp5",
            "synth_efinix",
            "synth_fabulous",
            "synth_gatemate",
            "synth_gowin",
            "synth_greepak4",
            "synth_ice40",
            "synth_intel",
            "synth_intel_alm",
            "synth_lattice",
            "synth_nexus",
            "synth_quicklogic",
            "synth_sf2",
            "synth_xilinx"
        ]);
        _yosysFlagSetting = new TitledSetting("Yosys Flags", "Set Yosys flags", defaultProperties.GetValueOrDefault("YosysToolchain_Yosys_Flags") ?? "");
        
        _nextPnrToolSetting = new ComboBoxSetting("NextPnr Tool", "Set NextPnr tool", defaultProperties.GetValueOrDefault("YosysToolchain_NextPnr_Tool") ?? "", [
            "nextpnr-ecp5",
            "nextpnr-generic",
            "nextpnr-gowin",
            "nextpnr-ice40",
            "nextpnr-machxo2",
            "nextpnr-nexus",
        ]);
        _nextPnrFlagSetting = new TitledSetting("NextPnR Flags", "Set NextPnr flags", defaultProperties.GetValueOrDefault("YosysToolchain_NextPnr_Flags") ?? "");
        
        _packToolSetting = new ComboBoxSetting("Pack Tool", "Set Pack tool", defaultProperties.GetValueOrDefault("YosysToolchain_Pack_Tool") ?? "", [
            "ecppack",
            "gowin_pack",
            "icepack",
        ]);
        _packToolFlagSetting = new TitledSetting("Pack Flags", "Set Pack flags", defaultProperties.GetValueOrDefault("YosysToolchain_Pack_Flags") ?? "");
        
        SettingsCollection.SettingModels.Add(new ComboBoxSettingViewModel(_yosysSynthToolSetting));
        SettingsCollection.SettingModels.Add(new TextBoxSettingViewModel(_yosysFlagSetting));
        
        SettingsCollection.SettingModels.Add(new ComboBoxSettingViewModel(_nextPnrToolSetting));
        SettingsCollection.SettingModels.Add(new TextBoxSettingViewModel(_nextPnrFlagSetting));
        
        SettingsCollection.SettingModels.Add(new ComboBoxSettingViewModel(_packToolSetting));
        SettingsCollection.SettingModels.Add(new TextBoxSettingViewModel(_packToolFlagSetting));

        if (_settings.TryGetValue("YosysToolchain_Yosys_SynthTool", out var yTool))
            _yosysSynthToolSetting.Value = yTool;
        if (_settings.TryGetValue("YosysToolchain_Yosys_Flags", out var yFlags))
            _yosysFlagSetting.Value = yFlags;
        
        if (_settings.TryGetValue("YosysToolchain_NextPnr_Tool", out var nTool))
            _nextPnrToolSetting.Value = nTool;
        if (_settings.TryGetValue("YosysToolchain_NextPnr_Flags", out var nFlags))
            _nextPnrFlagSetting.Value = nFlags;
        
        if (_settings.TryGetValue("YosysToolchain_Pack_Tool", out var pTool))
            _packToolSetting.Value = pTool;
        if (_settings.TryGetValue("YosysToolchain_Pack_Flags", out var pFlags))
            _packToolFlagSetting.Value = pFlags;
    }
    
    public void Save(FlexibleWindow flexibleWindow)
    {
        _settings["YosysToolchain_Yosys_SynthTool"] = _yosysSynthToolSetting.Value.ToString()!;
        _settings["YosysToolchain_Yosys_Flags"] = _yosysFlagSetting.Value.ToString()!;
        _settings["YosysToolchain_NextPnr_Tool"] = _nextPnrToolSetting.Value.ToString()!;
        _settings["YosysToolchain_NextPnr_Flags"] = _nextPnrFlagSetting.Value.ToString()!;
        _settings["YosysToolchain_Pack_Tool"] = _packToolSetting.Value.ToString()!;
        _settings["YosysToolchain_Pack_Flags"] = _packToolFlagSetting.Value.ToString()!;

        FpgaSettingsParser.SaveSettings(_fpgaProjectRoot, _selectedFpga.Name, _settings);
        
        Close(flexibleWindow);
    }

    public void Reset()
    {
        foreach (var setting in SettingsCollection.SettingModels)
        {
            setting.Setting.Value = setting.Setting.DefaultValue;
        }
    }
}