using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Extensions;
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
    private readonly ILogger _logger;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IProjectSettingsService _projectSettingsService;
    private readonly UniversalFpgaProjectRoot _root;

    /// <summary>
    ///     All categories loaded upfront: category name → (collection to display, key map for saving).
    /// </summary>
    private readonly Dictionary<string, (SettingsCollectionViewModel Collection, Dictionary<TitledSetting, string> Keys
            )>
        _categoryData = new();

    private string? _selectedCategory;
    private SettingsCollectionViewModel? _settingsCollection;

    public UniversalFpgaProjectSettingsEditorViewModel(UniversalFpgaProjectRoot root,
        IProjectExplorerService projectExplorerService, FpgaService fpgaService,
        IProjectSettingsService projectSettingsService, ILogger logger)
    {
        _root = root;
        _logger = logger;
        _projectExplorerService = projectExplorerService;
        _projectSettingsService = projectSettingsService;
        Title = $"{_root.Name} Settings";

        SettingCategories = new ObservableCollection<string>();
        _ = InitializeAsync();
    }

    public ObservableCollection<string> SettingCategories { get; }

    /// <summary>
    ///     The collection currently shown in the view. Changes when <see cref="SelectedCategory"/> changes.
    /// </summary>
    public SettingsCollectionViewModel? SettingsCollection
    {
        get => _settingsCollection;
        private set => SetProperty(ref _settingsCollection, value);
    }

    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            SetProperty(ref _selectedCategory, value);
            if (value != null && _categoryData.TryGetValue(value, out var data))
                SettingsCollection = data.Collection;
        }
    }

    private (SettingsCollectionViewModel Collection, Dictionary<TitledSetting, string> Keys) BuildCategoryCollection(
        string category)
    {
        var collection = new SettingsCollectionViewModel("") { ShowTitle = false };
        var keys = new Dictionary<TitledSetting, string>();

        foreach (var setting in _projectSettingsService.GetProjectSettingsList(category))
        {
            var local = setting.Setting;

            if (!setting.ActivationFunction(_root)) continue;

            // Pre-compile step checkboxes are backed by the shared preCompileSteps array
            if (local is CheckBoxSetting && setting.Key.StartsWith("preCompileStep_"))
            {
                var stepName = setting.Key["preCompileStep_".Length..];
                var enabledSteps = _root.Properties.GetStringArray("preCompileSteps")?.ToHashSet() ?? [];
                local.Value = enabledSteps.Contains(stepName);
                local.Priority = setting.Setting.Priority;
                keys.Add(local, setting.Key);
                collection.SettingModels.Add(local);
                continue;
            }

            if (_root.Properties.ContainsKey(setting.Key))
            {
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
                        local.Value = new ObservableCollection<string>(
                            _root.Properties[setting.Key]!.AsArray().Select(n => n!.ToString()));
                        break;

                    case AdvancedComboBoxSearchSetting:
                    case AdvancedComboBoxSetting:
                    case ComboBoxSearchSetting:
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

            keys.Add(local, setting.Key);
            collection.SettingModels.Add(local);
        }

        return (collection, keys);
    }

    private async Task InitializeAsync()
    {
        await SetupMenuAsync();

        foreach (var category in _projectSettingsService.GetProjectCategories())
            SettingCategories.Add(category);

        foreach (var category in SettingCategories)
            _categoryData[category] = BuildCategoryCollection(category);

        SelectedCategory = SettingCategories.FirstOrDefault();
    }

    private async Task SetupMenuAsync()
    {
        var vhdlStandard = new ComboBoxSetting("VHDL Standard",
            _root.Properties.GetString("vhdlStandard") ?? "",
            ["87", "93", "93c", "00", "02", "08", "19"])
        {
            MarkdownDocumentation =
                "The VHDL language standard version used when analysing and simulating VHDL files.\n\n" +
                "Common choices:\n- `08` — VHDL-2008 (recommended)\n- `93` — VHDL-93\n- `02` — VHDL-2002"
        };

        var includes = _root.Properties.GetStringArray("include")?.ToArray() ?? [];
        var exclude  = _root.Properties.GetStringArray("exclude")?.ToArray() ?? [];
        var compileExcluded = _root.Properties.GetStringArray("compileExcluded")?.ToArray() ?? [];

        var fpgaService = ContainerLocator.Container.Resolve<FpgaService>();

        var toolchains = fpgaService.Toolchains.Select(tc => tc.Id).ToArray<object>();
        var toolchain  = new ComboBoxSetting("Toolchain",
            _root.Properties.GetString("toolchain") ?? "", toolchains)
        {
            MarkdownDocumentation =
                "The synthesis and place-and-route toolchain used to compile this project.\n\n" +
                "The toolchain determines the full compile pipeline (synthesis, fit, assemble)."
        };

        var loaders = fpgaService.Loaders.Select(l => l.Id).ToArray<object>();
        var loader  = new ComboBoxSetting("Loader",
            _root.Properties.GetString("loader") ?? "", loaders)
        {
            MarkdownDocumentation =
                "The programming tool used to download the bitstream to the FPGA.\n\n" +
                "Examples: `openFPGALoader`, `iceprog`."
        };

        var includesSettings = new ListBoxSetting("Files to Include", includes)
        {
            MarkdownDocumentation =
                "Glob patterns or relative paths that are **explicitly included** in the project file set.\n\n" +
                "Leave empty to include all files in the project directory."
        };

        var excludesSettings = new ListBoxSetting("Files to Exclude", exclude)
        {
            MarkdownDocumentation =
                "Glob patterns or relative paths that are **excluded** from the project file set.\n\n" +
                "Excluded files are hidden from the project explorer and not passed to any tool."
        };

        var compileExcludedSettings = new ListBoxSetting("Compile Excluded", compileExcluded)
        {
            MarkdownDocumentation =
                "Relative paths of source files that are **excluded from compilation**.\n\n" +
                "The files remain visible in the project explorer but are not passed to the synthesiser/simulation."
        };

        // Async: scan project files for top-level entities
        var allEntities = await fpgaService.GetAllTopEntitiesAsync(_root);

        var entryOptions = allEntities.Select(x => new AdvancedComboBoxOption
        {
            Title = $"{x.TopEntity} ({x.File.RelativePath})",
            Value = x.TopEntity
        }).ToArray();

        var topEntitySetting = new AdvancedComboBoxSearchSetting("Top Entity", _root.TopEntity ?? "", entryOptions)
        {
            MarkdownDocumentation =
                "The top-level entity or module used for synthesis and pin planning.\n\n" +
                "This name must match the entity/module declaration in your HDL source files."
        };

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("topEntity")
                .WithSetting(topEntitySetting)
                .WithCategory("Project")
                .WithDisplayOrder(60)
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("toolchain")
                .WithCategory("Project")
                .WithDisplayOrder(70)
                .WithSetting(toolchain)
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("loader")
                .WithCategory("Project")
                .WithDisplayOrder(80)
                .WithSetting(loader)
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("vhdlStandard")
                .WithCategory("Project")
                .WithDisplayOrder(90)
                .WithSetting(vhdlStandard)
                .WithActivation(file =>
                {
                    if (file is UniversalFpgaProjectRoot root)
                        return root.GetFiles().Any(projectFile =>
                            Path.GetExtension(projectFile) is ".vhd" or ".vhdl");
                    return false;
                })
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("compileExcluded")
                .WithSetting(compileExcludedSettings)
                .WithCategory("Project")
                .WithDisplayOrder(100)
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("include")
                .WithSetting(includesSettings)
                .WithCategory("Files")
                .WithDisplayOrder(100)
                .Build()
        );

        _projectSettingsService.AddProjectSettingIfNotExists(
            new ProjectSettingBuilder()
                .WithKey("exclude")
                .WithSetting(excludesSettings)
                .WithCategory("Files")
                .WithDisplayOrder(110)
                .Build()
        );

        // Register a CheckBoxSetting for every registered pre-compile step
        var order = 120;
        foreach (var step in fpgaService.PreCompileSteps)
        {
            var key = $"preCompileStep_{step.Name}";
            var stepSetting = new CheckBoxSetting($"Pre-Compile: {step.Name}", false)
            {
                HoverDescription = $"Run '{step.Name}' as a pre-compile step before the toolchain.",
                MarkdownDocumentation =
                    $"When enabled, **{step.Name}** runs before the toolchain on every compile.\n\n" +
                    $"The step name is stored in the `preCompileSteps` array in the project file."
            };
            _projectSettingsService.AddProjectSettingIfNotExists(
                new ProjectSettingBuilder()
                    .WithKey(key)
                    .WithSetting(stepSetting)
                    .WithCategory("Project")
                    .WithDisplayOrder(order++)
                    .Build()
            );
        }
    }

    public async Task SaveAsync()
    {
        var enabledPreCompileSteps = new List<string>();

        foreach (var (_, (_, keys)) in _categoryData)
        foreach (var (setting, key) in keys)
        {
            // Pre-compile step checkboxes are saved collectively as the preCompileSteps array
            if (key.StartsWith("preCompileStep_"))
            {
                if (setting is CheckBoxSetting { Value: true })
                    enabledPreCompileSteps.Add(key["preCompileStep_".Length..]);
                continue;
            }

            _root.Properties.SetNode(key, CreateSettingNode(setting, key));
        }

        _root.Properties.SetNode("preCompileSteps",
            new JsonArray(enabledPreCompileSteps.Select(x => JsonValue.Create(x)).ToArray<JsonNode?>()));

        await _projectExplorerService.SaveProjectAsync(_root);
        await _projectExplorerService.ReloadProjectAsync(_root);
    }

    private static JsonNode? CreateSettingNode(TitledSetting setting, string key)
    {
        return setting switch
        {
            FolderPathSetting or FilePathSetting => JsonValue.Create((setting.Value as string)?.ToUnixPath()),
            ListBoxSetting when key is "include" or "exclude" => new JsonArray(
                ((ObservableCollection<string>)setting.Value)
                .Select(item => JsonValue.Create(item.ToUnixPath()))
                .ToArray()),
            _ => JsonValue.Create(setting.Value)
        };
    }

    public async Task SaveAndCloseAsync(FlexibleWindow window)
    {
        await SaveAsync();
        Close(window);
    }
}