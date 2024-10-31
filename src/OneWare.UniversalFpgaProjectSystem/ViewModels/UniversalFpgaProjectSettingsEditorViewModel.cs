using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows.Input;
using Avalonia.Controls;
using DynamicData;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.Settings.Views.SettingTypes;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;
using ReactiveUI;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectSettingsEditorViewModel : ReactiveObject
{
    public string Title { get; set; } = "Test";
    
    public List<UserControl> UserControlsList { get; }

    private UniversalFpgaProjectRoot _root;

    private ComboBoxSetting _toolchain;
    private ComboBoxSetting _loader;

    private ListBoxSetting _includesSettings;
    private ListBoxSetting _excludesSettings;
    
    public UniversalFpgaProjectSettingsEditorViewModel(UniversalFpgaProjectRoot root)
    {
        _root = root;
        Title = $"{_root.Name} Settings";
        
        var includes = _root.Properties["Include"]!.AsArray().Select(node => node!.ToString()).ToArray();
        var exclude = _root.Properties["Exclude"]!.AsArray().Select(node => node!.ToString()).ToArray();
        
        var toolchains = ContainerLocator.Container.Resolve<FpgaService>().Toolchains.Select(toolchain => toolchain.Name);
        var currentToolchain = _root.Properties["Toolchain"]!.ToString();
        _toolchain = new ComboBoxSetting("Toolchain", "test", currentToolchain, toolchains);
        
        var loader = ContainerLocator.Container.Resolve<FpgaService>().Loaders.Select(loader => loader.Name);
        var currentLoader = _root.Properties["Loader"]!.ToString();
        _loader = new ComboBoxSetting("Loader", "test", currentLoader, loader);

        _includesSettings = new ListBoxSetting("Files to Include", "test", includes);
        _excludesSettings = new ListBoxSetting("Files to Exclude", "test", exclude);
        
        UserControlsList = new List<UserControl>
        {
            new ComboBoxSettingView() {DataContext = new ComboBoxSettingViewModel(_toolchain) },
            new ComboBoxSettingView() {DataContext = new ComboBoxSettingViewModel(_loader) },
            new ListBoxSettingView() { DataContext = new ListBoxSettingViewModel(_includesSettings) },
            new ListBoxSettingView() { DataContext = new ListBoxSettingViewModel(_excludesSettings) }
        };
    }

    private async Task SaveAsync()
    {
        _root.Properties["Toolchain"] = _toolchain.Value.ToString();
        _root.Properties["Loader"] = _loader.Value.ToString();
        
        UpdateJsonArray(_root.Properties["Include"]!, _includesSettings.Items.Select(item => item.ToString()).ToArray());
        UpdateJsonArray(_root.Properties["Exclude"]!, _excludesSettings.Items.Select(item => item.ToString()).ToArray());
        
        await ContainerLocator.Container.Resolve<IProjectExplorerService>().SaveProjectAsync(_root);
    }

    private void UpdateJsonArray(JsonNode? jsonObject, string[] newValues)
    {
        jsonObject?.AsArray().Clear();
        foreach (var value in newValues)
        {
            jsonObject?.AsArray().Add(value);
        }
    }
    
    public async Task SaveAndCloseAsync(FlexibleWindow window)
    {
        await SaveAsync();
        Close(window);
    }
    
    
    public void Close(FlexibleWindow window)
    {
        window.Close();
    }
}