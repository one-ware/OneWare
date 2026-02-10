using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectSettingsEditorViewModel : FlexibleWindowViewModelBase
{
    private readonly FpgaService _fpgaService;

    private readonly ILogger _logger;
    private readonly IProjectExplorerService _projectExplorerService;

    private readonly IProjectSettingsService _projectSettingsService;

    private readonly Dictionary<TitledSetting, string> _dynamicSettingsKeys = new();

    private readonly UniversalFpgaProjectRoot _root;

    private string? _selectedCategory;

    public UniversalFpgaProjectSettingsEditorViewModel(UniversalFpgaProjectRoot root,
        IProjectExplorerService projectExplorerService, FpgaService fpgaService,
        IProjectSettingsService projectSettingsService, ILogger logger)
    {
        _root = root;
        _logger = logger;
        _projectExplorerService = projectExplorerService;
        _fpgaService = fpgaService;
        _projectSettingsService = projectSettingsService;
        Title = $"{_root.Name} Settings";

        SetupMenu();
        SettingCategories = new ObservableCollection<string>(projectSettingsService.GetProjectCategories());
        LoadSettingsForCategory(projectSettingsService.GetDefaultProjectCategory());
    }

    public SettingsCollectionViewModel SettingsCollection { get; } = new("")
    {
        ShowTitle = false
    };

    public ObservableCollection<string> SettingCategories { get; }

    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            SetProperty(ref _selectedCategory, value);
            if (value != null) LoadSettingsForCategory(value);
        }
    }


    private void LoadSettingsForCategory(string category)
    {
        SettingsCollection.Clear();
        _dynamicSettingsKeys.Clear();

        foreach (var setting in _projectSettingsService.GetProjectSettingsList(category))
        {
            var local = setting.Setting;

            if (!setting.ActivationFunction(_root)) continue;

            if (_root.Properties.ContainsKey(setting.Key))
            {
                // load stored value
                var node = _root.Properties[setting.Key];
                if (node == null) continue;

                switch (local)
                {
                    case CheckBoxSetting:
	                    local.Value = _root.Properties[setting.Key]!.ToString() == "True";
                        break;

                    case FolderPathSetting:
                    case FilePathSetting:
                    case TextBoxSetting:
                        local.Value = _root.Properties[setting.Key]!.ToString();
                        break;

                    case ListBoxSetting:
	                    local.Value = new ObservableCollection<string>(_root.Properties[setting.Key]!.AsArray().Select(node => node!.ToString()));
                        break;

                    case ComboBoxSearchSetting:
	                    local.Value = _root.Properties[setting.Key]!.ToString();
                        break;
                    
                    case ComboBoxSetting:
                        local.Value = _root.Properties[setting.Key]!.ToString();
                        break;

                    case SliderSetting:
	                    local.Value = double.Parse(_root.Properties[setting.Key]!.ToString(),
		                    CultureInfo.InvariantCulture);
                        break;
                    
                    case ColorSetting:
                        Color.TryParse(_root.Properties[setting.Key]!.ToString(), out var color);
                        local.Value = color;
                        break;

                    default:
                        _logger.Error($"Unknown setting of type: {local.GetType().Name}");
                        continue;
                }

                local.Priority = setting.Setting.Priority;
            }
            else
            {
	            local.Value = local.DefaultValue;
            }

            _dynamicSettingsKeys.Add(local, setting.Key);
            SettingsCollection.SettingModels.Add(local);
        }

        // OnPropertyChanged();
    }

    private void SetupMenu()
    {
        var value = _root.Properties.GetString("vhdlStandard") ?? "";
        var vhdlStandard = new ComboBoxSetting("VHDL Standard", value, ["87", "93", "93c", "00", "02", "08", "19"]);

        var includes = _root.Properties.GetStringArray("include")?.ToArray() ?? [];
        var exclude = _root.Properties.GetStringArray("exclude")?.ToArray() ?? [];

        var toolchains = ContainerLocator.Container.Resolve<FpgaService>().Toolchains
            .Select(toolchain => toolchain.Id)
            .ToArray();
        var currentToolchain = _root.Properties.GetString("toolchain") ?? "";
        var toolchain = new ComboBoxSetting("Toolchain", currentToolchain, toolchains);

        var loaders = ContainerLocator.Container.Resolve<FpgaService>().Loaders
            .Select(loader => loader.Id)
            .ToArray();
        var currentLoader = _root.Properties.GetString("loader") ?? "";
        var loader = new ComboBoxSetting("Loader", currentLoader, loaders);

        var includesSettings = new ListBoxSetting("Files to Include", includes);
        var excludesSettings = new ListBoxSetting("Files to Exclude", exclude);

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("toolchain")
                .WithSetting(toolchain)
                .WithDisplayOrder(100)
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("loader")
                .WithDisplayOrder(90)
                .WithSetting(loader)
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("vhdlStandard")
                .WithDisplayOrder(80)
                .WithSetting(vhdlStandard)
                .WithActivation(file =>
                {
                    if (file is UniversalFpgaProjectRoot root)
                    {
                        if (root.TopEntity is not null) return Path.GetExtension(root.TopEntity) is ".vhd" or ".vhdl";
                        return root.GetFiles().Any(projectFile => Path.GetExtension(projectFile) is ".vhd" or ".vhdl");
                    }

                    return false;
                })
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("include")
                .WithSetting(includesSettings)
                .WithDisplayOrder(200)
                .WithCategory("Project")
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("exclude")
                .WithSetting(excludesSettings)
                .WithDisplayOrder(100)
                .WithCategory("Project")
                .Build()
        );
    }

    public async Task SaveAsync()
    {
        foreach (var (setting, key) in _dynamicSettingsKeys)
        {
            _root.Properties.SetNode(key, JsonValue.Create(setting.Value));
        }

        await _projectExplorerService.SaveProjectAsync(_root);
        await _projectExplorerService.ReloadProjectAsync(_root);
    }

    public async Task SaveAndCloseAsync(FlexibleWindow window)
    {
        await SaveAsync();
        Close(window);
    }
}
