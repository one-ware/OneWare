using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
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

    private readonly ILogger _logger;

    public SettingsCollectionViewModel SettingsCollection { get; } = new("")
    {
        ShowTitle = false
    };

    private UniversalFpgaProjectRoot _root;
    
    private Dictionary<TitledSetting, string> _dynamicSettingsKeys = new();

    public ObservableCollection<string> SettingCategories { get; }
    
    private string? _selectedCategory;
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            SetProperty(ref _selectedCategory, value);
            if (value != null)
            {
                LoadSettingsForCategory(value);
            }
        }
    }

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


    private void LoadSettingsForCategory(string category)
    {
        SettingsCollection.Clear();
        _dynamicSettingsKeys.Clear();
        
        foreach (ProjectSetting setting in _projectSettingsService.GetProjectSettingsList(category))
        {
            TitledSetting localCopy = setting.Setting;

            if (setting.ActivationFunction(_root) == false)
            {
                continue;
            }

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
                            Double.Parse(_root.Properties[setting.Key]!.ToString(), CultureInfo.InvariantCulture), ((SliderSetting)localCopy).Min,
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

                        _logger.Error($"Unknown setting of type: {localCopy.GetType().Name}");
                        continue;
                }
                localCopy.Priority = setting.Setting.Priority;
            }
            
            _dynamicSettingsKeys.Add(localCopy, setting.Key);
            SettingsCollection.SettingModels.Add(localCopy);
            
        }
        
        // OnPropertyChanged();
    }

    private void SetupMenu()
    {
        var value = _root.Properties["VHDL_Standard"] == null ? "" : _root.Properties["VHDL_Standard"]!.ToString();
        ComboBoxSetting vhdlStandard = new ComboBoxSetting("VHDL Standard", value, ["87", "93", "93c", "00", "02", "08", "19"]);
        
        var includes = _root.Properties["Include"]!.AsArray().Select(node => node!.ToString()).ToArray();
        var exclude = _root.Properties["Exclude"]!.AsArray().Select(node => node!.ToString()).ToArray();

        var toolchains = ContainerLocator.Container.Resolve<FpgaService>().Toolchains
            .Select(toolchain => toolchain.Name);
        var currentToolchain = _root.Properties["Toolchain"]!.ToString();
        var toolchain = new ComboBoxSetting("Toolchain", currentToolchain, toolchains);

        var loaders = ContainerLocator.Container.Resolve<FpgaService>().Loaders.Select(loader => loader.Name);
        var currentLoader = _root.Properties["Loader"]!.ToString();
        var loader = new ComboBoxSetting("Loader", currentLoader, loaders);

        var includesSettings = new ListBoxSetting("Files to Include", includes);
        var excludesSettings = new ListBoxSetting("Files to Exclude", exclude);

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("Toolchain")
                .WithSetting(toolchain)
                .WithDisplayOrder(100)
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("Loader")
                .WithDisplayOrder(90)
                .WithSetting(loader)
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("VHDL_Standard")
                .WithDisplayOrder(80)
                .WithSetting(vhdlStandard)
                .WithActivation(file =>
                {
                    if (file is UniversalFpgaProjectRoot root)
                    {
                        if (root.TopEntity is not null)
                        {
                            return Path.GetExtension(root.TopEntity.FullPath) is ".vhd";
                        }
                        return root.Files.Exists(projectFile => Path.GetExtension(projectFile.FullPath) is ".vhd");
                    }
                    return false;
                })
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("Include")
                .WithSetting(includesSettings)
                .WithDisplayOrder(200)
                .WithCategory("Project")
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("Exclude")
                .WithSetting(excludesSettings)
                .WithDisplayOrder(100)
                .WithCategory("Project")
                .Build()
        );
    }
    
    public async Task SaveAsync()
    {
        foreach (var toSave in SettingsCollection.SettingModels)
        {
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