using Avalonia.Media;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectSettingsEditorViewModel : FlexibleWindowViewModelBase
{
    private readonly IProjectExplorerService _projectExplorerService;

    private readonly FpgaService _fpgaService;

    private readonly IProjectSettingsService _projectSettingsService;

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

    public UniversalFpgaProjectSettingsEditorViewModel(UniversalFpgaProjectRoot root,
        IProjectExplorerService projectExplorerService, FpgaService fpgaService,
        IProjectSettingsService projectSettingsService, ILogger logger)
    {
        _root = root;
        _projectExplorerService = projectExplorerService;
        _fpgaService = fpgaService;
        _projectSettingsService = projectSettingsService;
        Title = $"{_root.Name} Settings";

        if (root.TopEntity != null && (root.TopEntity.Name.Contains("vhd") || root.TopEntity.Name.Contains("vhdl")))
        {
            var standard = _root.Properties["VHDL_Standard"];
            var value = standard == null ? "" : standard.ToString();
            _vhdlStandard = new ComboBoxSetting("VHDL Standard", value, ["87", "93", "93c", "00", "02", "08", "19"]);
        }

        var includes = _root.Properties["Include"]!.AsArray().Select(node => node!.ToString()).ToArray();
        var exclude = _root.Properties["Exclude"]!.AsArray().Select(node => node!.ToString()).ToArray();

        var toolchains = ContainerLocator.Container.Resolve<FpgaService>().Toolchains
            .Select(toolchain => toolchain.Name);
        var currentToolchain = _root.Properties["Toolchain"]!.ToString();
        _toolchain = new ComboBoxSetting("Toolchain", currentToolchain, toolchains);

        var loader = ContainerLocator.Container.Resolve<FpgaService>().Loaders.Select(loader => loader.Name);
        var currentLoader = _root.Properties["Loader"]!.ToString();
        _loader = new ComboBoxSetting("Loader", currentLoader, loader);

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

            if (_root.Properties.AsObject().ContainsKey(setting.Key))
            {
                // load stored value

                switch (localCopy.GetType().Name)
                {
                    case "CheckBoxSetting":
                        localCopy = new CheckBoxSetting(localCopy.Title,
                            _root.Properties[setting.Key]!.ToString() == "True" ? true : false);
                        break;

                    case "TextBoxSetting":
                        localCopy = new TextBoxSetting(localCopy.Title, _root.Properties[setting.Key]!.ToString(),
                            ((TextBoxSetting)localCopy).Watermark);
                        break;

                    case "ComboBoxSetting":
                        localCopy = new ComboBoxSetting(localCopy.Title, _root.Properties[setting.Key]!.ToString(),
                            ((ComboBoxSetting)localCopy).Options);
                        break;

                    case "ListBoxSetting":
                        localCopy = new ListBoxSetting(localCopy.Title,
                            _root.Properties[setting.Key]!.AsArray().Select(node => node!.ToString()).ToArray());
                        break;

                    case "ComboBoxSearchSetting":
                        localCopy = new ComboBoxSearchSetting(localCopy.Title,
                            _root.Properties[setting.Key]!.AsArray().Select(node => node!.ToString()).ToArray(),
                            ((ComboBoxSearchSetting)localCopy).Options);
                        break;

                    case "SliderSetting":
                        localCopy = new SliderSetting(localCopy.Title,
                            (double)_root.Properties[setting.Key]!.AsValue(), ((SliderSetting)localCopy).Min,
                            ((SliderSetting)localCopy).Max, ((SliderSetting)localCopy).Step);
                        break;

                    case "FolderPathSetting":
                        localCopy = new FolderPathSetting(localCopy.Title,
                            _root.Properties[setting.Key]!.ToString(), ((FolderPathSetting)localCopy).Watermark,
                            ((FolderPathSetting)localCopy).StartDirectory, ((FolderPathSetting)localCopy).CheckPath);
                        break;

                    case "FilePathSetting":
                        localCopy = new FilePathSetting(localCopy.Title,
                            _root.Properties[setting.Key]!.ToString(),
                            ((FilePathSetting)localCopy).Watermark, ((FilePathSetting)localCopy).StartDirectory,
                            ((FilePathSetting)localCopy).CheckPath);
                        break;

                    case "ColorSetting":
                        Color.TryParse(_root.Properties[setting.Key]!.ToString(), out Color color);
                        localCopy = new ColorSetting(localCopy.Title, color);
                        break;

                    default:

                        logger.Error($"Unknown setting of type: {localCopy.GetType().Name}");
                        continue;
                }
            }

            _dynamicSettingsKeys.Add(localCopy, setting.Key);

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
            if (toSave == _toolchain || toSave == _loader || toSave == _includesSettings ||
                toSave == _excludesSettings || toSave == _vhdlStandard)
            {
                // Hardcoded; Skip for now

                continue;
            }

            if (toSave is not TitledSetting)
            {
                continue;
            }

            switch (toSave.GetType().Name)
            {
                case "CheckBoxSetting":
                    _root.SetProjectProperty(_dynamicSettingsKeys[(toSave as TitledSetting)!],
                        toSave.Value.ToString()!);
                    break;

                case "TextBoxSetting":
                    _root.SetProjectProperty(_dynamicSettingsKeys[(toSave as TitledSetting)!], toSave.Value.ToString()!);
                    break;

                case "ComboBoxSetting":
                    _root.SetProjectProperty(_dynamicSettingsKeys[(toSave as TitledSetting)!], toSave.Value.ToString()!);
                    break;

                case "ListBoxSetting":
                    _root.SetProjectPropertyArray(_dynamicSettingsKeys[(toSave as TitledSetting)!], ((ListBoxSetting)toSave).Items.Select(item => item.ToString()).ToArray());
                    break;

                case "ComboBoxSearchSetting":
                    _root.SetProjectProperty(_dynamicSettingsKeys[(toSave as TitledSetting)!], toSave.Value.ToString()!);
                    break;

                case "SliderSetting":
                    _root.SetProjectProperty(_dynamicSettingsKeys[(toSave as TitledSetting)!], toSave.Value.ToString()!);
                    break;

                case "FolderPathSetting":
                    _root.SetProjectProperty(_dynamicSettingsKeys[(toSave as TitledSetting)!], toSave.Value.ToString()!);
                    break;

                case "FilePathSetting":
                    _root.SetProjectProperty(_dynamicSettingsKeys[(toSave as TitledSetting)!], toSave.Value.ToString()!);
                    break;

                case "ColorSetting":
                    _root.SetProjectProperty(_dynamicSettingsKeys[(toSave as TitledSetting)!], ((Color) ((ColorSetting) toSave).Value).ToString());
                    break;

                default:
                    // This should never happen
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