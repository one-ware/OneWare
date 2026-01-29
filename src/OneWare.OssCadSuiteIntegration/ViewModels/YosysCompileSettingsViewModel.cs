using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;

namespace OneWare.OssCadSuiteIntegration.ViewModels;

public class YosysCompileSettingsViewModel : FlexibleWindowViewModelBase
{
    private readonly UniversalFpgaProjectRoot _fpgaProjectRoot;
    
    private readonly TextBoxSetting _yosysCommandSetting;
    private readonly TextBoxSetting _yosysFlagSetting;
    private readonly ComboBoxSetting _yosysSynthToolSetting;
    private readonly CheckBoxSetting _yosysQuietFlagSetting;
    
    private readonly TextBoxSetting _nextPnrFlagSetting;
    private readonly ComboBoxSetting _nextPnrToolSetting;
    private readonly ComboBoxSetting _nextPnrToolConstrainFileSetting;
    private readonly ComboBoxSetting _nextPnrToolOutputTypeSetting;
    private readonly TextBoxSetting _packToolFlagSetting;
    private readonly ComboBoxSetting _packToolSetting;
    private readonly IFpga _selectedFpga;
    private readonly Dictionary<string, string> _settings;
    
    private readonly ComboBoxSetting _packOutputTypeSetting;



    public YosysCompileSettingsViewModel(UniversalFpgaProjectRoot fpgaProjectRoot, IFpga selectedFpga)
    {
        _fpgaProjectRoot = fpgaProjectRoot;
        _selectedFpga = selectedFpga;

        Title = "Yosys Compile Settings";
        Id = "YosysCompileSettings";

        var defaultProperties = _selectedFpga.Properties;
        _settings = FpgaSettingsParser.LoadSettings(fpgaProjectRoot, _selectedFpga.Name);

        //Add missing default properties
        foreach (var property in defaultProperties) _settings.TryAdd(property.Key, property.Value);

        _yosysSynthToolSetting = new ComboBoxSetting("Yosys Synth Tool",
            defaultProperties.GetValueOrDefault("yosysToolchainYosysSynthTool") ?? "", [
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
                "synth_xilinx",
                "synth_gatemate"
            ])
        {
            HoverDescription = "Set Yosys Synth tool"
        };
        
        _yosysCommandSetting = new TextBoxSetting("Yosys Command",
            defaultProperties.GetValueOrDefault("yosysToolchainCommand") ?? "", null)
        {
            HoverDescription = "Set Yosys Command"
        };
        
        _yosysFlagSetting = new TextBoxSetting("Yosys Flags",
            defaultProperties.GetValueOrDefault("yosysToolchainYosysFlags") ?? "", null)
        {
            HoverDescription = "Set Yosys flags"
        };

        _yosysQuietFlagSetting = new CheckBoxSetting("Yosys Verbose", 
            Boolean.Parse(defaultProperties.GetValueOrDefault("yosysQuietFlag") ?? "true"));

        _nextPnrToolSetting = new ComboBoxSetting("NextPnr Tool",
            defaultProperties.GetValueOrDefault("yosysToolchainNextPnrTool") ?? "", [
                "nextpnr-ecp5",
                "nextpnr-generic",
                "nextpnr-gowin",
                "nextpnr-ice40",
                "nextpnr-machxo2",
                "nextpnr-nexus",
                "nextpnr-himbaechel"
            ])
        {
            HoverDescription = "Set NextPnr tool"
        };

        _nextPnrFlagSetting = new TextBoxSetting("NextPnR Flags",
            defaultProperties.GetValueOrDefault("yosysToolchainNextPnrFlags") ?? "", null)
        {
            HoverDescription = "Set NextPnr flags"
        };
        
        _nextPnrToolConstrainFileSetting = new ComboBoxSetting("NextPnr constraint file type",
            defaultProperties.GetValueOrDefault("yosysToolchainConstraintFileType") ?? "", [
                "pcf",
                "ccf",
            ])
        {
            HoverDescription = "Set NextPnr tool"
        };
        
        _nextPnrToolOutputTypeSetting = new ComboBoxSetting("NextPnr Output file type",
            defaultProperties.GetValueOrDefault("yosysToolchainOutputType") ?? "", [
                "asc",
                "txt"
            ])
        {
            HoverDescription = "Set NextPnr tool"
        };
        
        _packToolSetting = new ComboBoxSetting("Pack Tool",
            defaultProperties.GetValueOrDefault("yosysToolchainPackTool") ?? "", [
                "ecppack",
                "gowin_pack",
                "icepack",
                "gmpack"
            ])
        {
            HoverDescription = "Set Pack tool"
        };

        _packToolFlagSetting = new TextBoxSetting("Pack Flags",
            defaultProperties.GetValueOrDefault("yosysToolchainPackFlags") ?? "", null)
        {
            HoverDescription = "Set Pack flags"
        };
        
        _packOutputTypeSetting = new ComboBoxSetting("Pack output format",
            defaultProperties.GetValueOrDefault("packToolOutputFormat") ?? "", [
                "bin",
                "bit"
            ])
        {
            HoverDescription = "Set Pack tool output format"
        };
        
        SettingsCollection.SettingModels.Add(_yosysSynthToolSetting);
        SettingsCollection.SettingModels.Add(_yosysFlagSetting);
        SettingsCollection.SettingModels.Add(_yosysCommandSetting);
        SettingsCollection.SettingModels.Add(_yosysQuietFlagSetting);

        SettingsCollection.SettingModels.Add(_nextPnrToolSetting);
        SettingsCollection.SettingModels.Add(_nextPnrFlagSetting);
        SettingsCollection.SettingModels.Add(_nextPnrToolConstrainFileSetting);
        SettingsCollection.SettingModels.Add(_nextPnrToolOutputTypeSetting);

        SettingsCollection.SettingModels.Add(_packToolSetting);
        SettingsCollection.SettingModels.Add(_packToolFlagSetting);
        SettingsCollection.SettingModels.Add(_packOutputTypeSetting);

        if (_settings.TryGetValue("yosysToolchainYosysSynthTool", out var yTool))
            _yosysSynthToolSetting.Value = yTool;
        if (_settings.TryGetValue("yosysToolchainYosysFlags", out var yFlags))
            _yosysFlagSetting.Value = yFlags;
        if (_settings.TryGetValue("yosysToolchainCommand", out var yCommand))
            _yosysCommandSetting.Value = yCommand;
        if (_settings.TryGetValue("yosysQuietFlag", out var yQuiet))
            _yosysQuietFlagSetting.Value = yQuiet;

        if (_settings.TryGetValue("yosysToolchainNextPnrTool", out var nTool))
            _nextPnrToolSetting.Value = nTool;
        if (_settings.TryGetValue("yosysToolchainNextPnrFlags", out var nFlags))
            _nextPnrFlagSetting.Value = nFlags;
        if (_settings.TryGetValue("yosysToolchainConstraintFileType", out var nConstrain))
            _nextPnrToolConstrainFileSetting.Value = nConstrain;
        if (_settings.TryGetValue("yosysToolchainOutputType", out var nOutput))
            _nextPnrToolOutputTypeSetting.Value = nOutput;

        if (_settings.TryGetValue("yosysToolchainPackTool", out var pTool))
            _packToolSetting.Value = pTool;
        if (_settings.TryGetValue("yosysToolchainPackFlags", out var pFlags))
            _packToolFlagSetting.Value = pFlags;
        if (_settings.TryGetValue("packToolOutputFormat", out var pOutput))
            _packOutputTypeSetting.Value = pOutput;
    }

    public SettingsCollectionViewModel SettingsCollection { get; } = new("Yosys Settings")
    {
        ShowTitle = false
    };

    public void Save(FlexibleWindow flexibleWindow)
    {
        _settings["yosysToolchainYosysSynthTool"] = _yosysSynthToolSetting.Value.ToString()!;
        _settings["yosysToolchainYosysFlags"] = _yosysFlagSetting.Value.ToString()!;
        _settings["yosysToolchainNextPnrTool"] = _nextPnrToolSetting.Value.ToString()!;
        _settings["yosysToolchainNextPnrFlags"] = _nextPnrFlagSetting.Value.ToString()!;
        _settings["yosysToolchainPackTool"] = _packToolSetting.Value.ToString()!;
        _settings["yosysToolchainPackFlags"] = _packToolFlagSetting.Value.ToString()!;
        _settings["yosysToolchainConstraintFileType"] = _nextPnrToolConstrainFileSetting.Value.ToString()!;
        _settings["yosysToolchainNextPnrInput"] = _nextPnrToolConstrainFileSetting.Value.ToString()!;
        _settings["yosysToolchainCommand"] = _yosysCommandSetting.Value.ToString()!;
        _settings["yosysToolchainOutputType"] = _nextPnrToolOutputTypeSetting.Value.ToString()!;
        _settings["packToolOutputFormat"] = _packOutputTypeSetting.Value.ToString()!;
        _settings["yosysQuietFlag"] = _yosysQuietFlagSetting.Value.ToString()!;

        FpgaSettingsParser.SaveSettings(_fpgaProjectRoot, _selectedFpga.Name, _settings);

        Close(flexibleWindow);
    }

    public void Reset()
    {
        foreach (var setting in SettingsCollection.SettingModels) setting.Value = setting.DefaultValue;
    }
}