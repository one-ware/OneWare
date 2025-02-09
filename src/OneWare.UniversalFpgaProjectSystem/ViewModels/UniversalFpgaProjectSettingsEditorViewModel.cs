using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows.Input;
using Avalonia.Controls;
using DynamicData;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.Settings.Views.SettingTypes;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectSettingsEditorViewModel : FlexibleWindowViewModelBase
{
    private readonly IProjectExplorerService _projectExplorerService;
    
    private readonly FpgaService _fpgaService;
    
    public SettingsCollectionViewModel SettingsCollection { get; } = new("")
    {
        ShowTitle = false
    };

    private UniversalFpgaProjectRoot _root;

    private ComboBoxSetting _toolchain;
    private ComboBoxSetting _loader;
    private ComboBoxSetting? _vhdlStandard;

    private ListBoxSetting _includesSettings;
    private ListBoxSetting _excludesSettings;
    
    public UniversalFpgaProjectSettingsEditorViewModel(UniversalFpgaProjectRoot root, IProjectExplorerService projectExplorerService, FpgaService fpgaService)
    {
        _root = root;
        _projectExplorerService = projectExplorerService;
        _fpgaService = fpgaService;
        Title = $"{_root.Name} Settings";

        if (root.TopEntity != null && (root.TopEntity.Name.Contains("vhd") || root.TopEntity.Name.Contains("vhdl")))
        {
            var standard = _root.Properties["VHDL_Standard"];
            var value = standard == null ? "" : standard.ToString();
            _vhdlStandard = new ComboBoxSetting("VHDL Standard", value, ["87", "93", "93c", "00", "02", "08", "19"]);
        }
        
        var includes = _root.Properties["Include"]!.AsArray().Select(node => node!.ToString()).ToArray();
        var exclude = _root.Properties["Exclude"]!.AsArray().Select(node => node!.ToString()).ToArray();
        
        var toolchains = ContainerLocator.Container.Resolve<FpgaService>().Toolchains.Select(toolchain => toolchain.Name);
        var currentToolchain = _root.Properties["Toolchain"]!.ToString();
        _toolchain = new ComboBoxSetting("Toolchain", currentToolchain, toolchains);
        
        var loader = ContainerLocator.Container.Resolve<FpgaService>().Loaders.Select(loader => loader.Name);
        var currentLoader = _root.Properties["Loader"]!.ToString();
        _loader = new ComboBoxSetting("Loader", currentLoader, loader);
    
        _includesSettings = new ListBoxSetting("Files to Include", includes);
        _excludesSettings = new ListBoxSetting("Files to Exclude", exclude);
        
        SettingsCollection.SettingModels.Add(_toolchain);
        SettingsCollection.SettingModels.Add(_loader);
        
        if (_vhdlStandard != null) {
            SettingsCollection.SettingModels.Add(_vhdlStandard);
        }
        
        SettingsCollection.SettingModels.Add(_includesSettings);
        SettingsCollection.SettingModels.Add(_excludesSettings);
        
    }

    private async Task SaveAsync()
    {
        var tcString = _toolchain.Value.ToString();
        if (tcString != null && _fpgaService.Toolchains.FirstOrDefault(x => x.Name == tcString) is { } tc)
            _root.Toolchain = tc;
        
        var loaderString = _loader.Value.ToString();
        if (loaderString != null && _fpgaService.Loaders.FirstOrDefault(x => x.Name == loaderString) is { } loader)
            _root.Loader = loader;
        
        _root.SetProjectPropertyArray("Include", _includesSettings.Items.Select(item => item.ToString()).ToArray());
        _root.SetProjectPropertyArray("Exclude", _excludesSettings.Items.Select(item => item.ToString()).ToArray());
        
        if (_vhdlStandard?.Value.ToString() != null)
            _root.SetProjectProperty("VHDL_Standard", _vhdlStandard.Value.ToString()!);
        
        await _projectExplorerService.SaveProjectAsync(_root);
        await _projectExplorerService.ReloadAsync(_root);
    }
    
    public async Task SaveAndCloseAsync(FlexibleWindow window)
    {
        await SaveAsync();
        Close(window);
    }
}