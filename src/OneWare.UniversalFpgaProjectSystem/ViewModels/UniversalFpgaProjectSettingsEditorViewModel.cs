using System.Globalization;
using Avalonia.Media;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectSettingsEditorViewModel : FlexibleWindowViewModelBase
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly FpgaService _fpgaService;
    private readonly IProjectSettingsService _projectSettingsService;
    private readonly ILogger _logger;

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

    private Dictionary<TitledSetting, string> _dynamicSettingsKeys = new();

    public UniversalFpgaProjectSettingsEditorViewModel(
        UniversalFpgaProjectRoot root,
        IProjectExplorerService projectExplorerService,
        FpgaService fpgaService,
        IProjectSettingsService projectSettingsService,
        ILogger logger)
    {
        _root = root;
        _projectExplorerService = projectExplorerService;
        _fpgaService = fpgaService;
        _projectSettingsService = projectSettingsService;
        _logger = logger;

        Title = $"{_root.Name} Settings";

        if (root.TopEntity != null && (root.TopEntity.Name.Contains("vhd") || root.TopEntity.Name.Contains("vhdl")))
        {
            var standard = _root.Properties["VHDL_Standard"];
            var value = standard?.ToString() ?? "";
            _vhdlStandard = new ComboBoxSetting("VHDL Standard", value, ["87", "93", "93c", "00", "02", "08", "19"]);
        }

        var includes = _root.Properties["Include"]!.AsArray().Select(node => node!.ToString()).ToArray();
        var exclude = _root.Properties["Exclude"]!.AsArray().Select(node => node!.ToString()).ToArray();

        var toolchains = _fpgaService.Toolchains.Select(toolchain => toolchain.Name);
        var currentToolchain = _root.Properties["Toolchain"]!.ToString();
        _toolchain = new ComboBoxSetting("Toolchain", currentToolchain, toolchains);

        var loaders = _fpgaService.Loaders.Select(loader => loader.Name);
        var currentLoader = _root.Properties["Loader"]!.ToString();
        _loader = new ComboBoxSetting("Loader", currentLoader, loaders);

        _includesSettings = new ListBoxSetting("Files to Include", includes);
        _excludesSettings = new ListBoxSetting("Files to Exclude", exclude);

        SettingsCollection.SettingModels.Add(_toolchain);
        SettingsCollection.SettingModels.Add(_loader);

        if (_vhdlStandard != null)
        {
            SettingsCollection.SettingModels.Add(_vhdlStandard);
        }

        SettingsCollection.SettingModels.Add(_includesSettings);
        SettingsCollection.SettingModels.Add(_excludesSettings);

        foreach (ProjectSetting setting in _projectSettingsService.GetProjectSettingsList())
        {
            TitledSetting localCopy = setting.Setting;

            if (!setting.ActivationFunction(root))
                continue;

            if (_root.Properties.AsObject().ContainsKey(setting.Key))
            {
                var value = _root.Properties[setting.Key]!.ToString();

                localCopy = localCopy switch
                {
                    CheckBoxSetting => new CheckBoxSetting(localCopy.Title, value == "True"),
                    TextBoxSetting text => new TextBoxSetting(localCopy.Title, value, text.Watermark),
                    ComboBoxSetting combo => new ComboBoxSetting(localCopy.Title, value, combo.Options),
                    ListBoxSetting => new ListBoxSetting(localCopy.Title,
                        _root.Properties[setting.Key]!.AsArray().Select(n => n!.ToString()).ToArray()),
                    ComboBoxSearchSetting comboSearch => new ComboBoxSearchSetting(localCopy.Title,
                        _root.Properties[setting.Key]!.AsArray().Select(n => n!.ToString()).ToArray(),
                        comboSearch.Options),
                    SliderSetting slider => new SliderSetting(localCopy.Title,
                        double.Parse(value, CultureInfo.InvariantCulture), slider.Min, slider.Max, slider.Step),
                    FolderPathSetting folder => new FolderPathSetting(localCopy.Title, value,
                        folder.Watermark, folder.StartDirectory, folder.CheckPath),
                    FilePathSetting file => new FilePathSetting(localCopy.Title, value,
                        file.Watermark, file.StartDirectory, file.CheckPath),
                    ColorSetting => Color.TryParse(value, out var color)
                        ? new ColorSetting(localCopy.Title, color)
                        : localCopy,
                    _ => _logger.Error($"Unknown setting of type: {localCopy.GetType().Name}"),
                };
            }

            _dynamicSettingsKeys[localCopy] = setting.Key;
            SettingsCollection.SettingModels.Add(localCopy);
        }
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

        foreach (var toSave in SettingsCollection.SettingModels)
        {
            if (toSave is not TitledSetting ts ||
                toSave == _toolchain || toSave == _loader || toSave == _includesSettings ||
                toSave == _excludesSettings || toSave == _vhdlStandard)
                continue;

            switch (toSave)
            {
                case ListBoxSetting listBox:
                    _root.SetProjectPropertyArray(_dynamicSettingsKeys[ts],
                        listBox.Items.Select(i => i.ToString()).ToArray());
                    break;

                case ColorSetting colorSetting:
                    _root.SetProjectProperty(_dynamicSettingsKeys[ts], ((Color)colorSetting.Value).ToString());
                    break;

                default:
                    _root.SetProjectProperty(_dynamicSettingsKeys[ts], toSave.Value.ToString()!);
                    break;
            }
        }

        await _projectExplorerService.SaveProjectAsync(_root);
        await _projectExplorerService.ReloadAsync(_root);
    }

    public async Task SaveAndCloseAsync(FlexibleWindow window)
    {
        await SaveAsync();
        Close(window);
    }
}
