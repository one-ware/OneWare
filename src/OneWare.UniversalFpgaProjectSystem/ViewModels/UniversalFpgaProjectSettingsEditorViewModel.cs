using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Extensions;
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

    private readonly Dictionary<string, (SettingsCollectionViewModel Collection, Dictionary<TitledSetting, string> Keys)>
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

    private async Task InitializeAsync()
    {
        foreach (var category in _projectSettingsService.GetProjectCategories())
            SettingCategories.Add(category);

        foreach (var category in SettingCategories)
            _categoryData[category] = await BuildCategoryCollectionAsync(category);

        AddMissingPreCompileStepDummies();

        SelectedCategory = SettingCategories.FirstOrDefault();
    }

    /// <summary>
    /// For every step name stored in the project's <c>preCompileSteps</c> array that has no
    /// corresponding registered <c>preCompileStep_*</c> setting (because the plugin is disabled),
    /// inject a checked dummy <see cref="CheckBoxSetting"/> into the Project category so the
    /// entry is preserved and visible.
    /// </summary>
    private void AddMissingPreCompileStepDummies()
    {
        var enabledSteps = _root.Properties.GetStringArray("preCompileSteps")?.ToHashSet() ?? [];
        if (enabledSteps.Count == 0) return;

        // Collect step names already covered by registered settings
        var coveredSteps = _categoryData.Values
            .SelectMany(v => v.Keys.Values)
            .Where(k => k.StartsWith("preCompileStep_"))
            .Select(k => k["preCompileStep_".Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingSteps = enabledSteps.Where(s => !coveredSteps.Contains(s)).ToList();
        if (missingSteps.Count == 0) return;

        if (!_categoryData.TryGetValue("Project", out var projectData)) return;
        var (collection, keys) = projectData;

        foreach (var stepName in missingSteps)
        {
            var dummy = new CheckBoxSetting($"Pre-Compile: {stepName} (plugin unavailable)", true)
            {
                HoverDescription =
                    $"The plugin providing the '{stepName}' pre-compile step is not currently loaded. " +
                    "The step remains configured in your project and will run when the plugin is re-enabled.",
                Priority = 250
            };
            keys.Add(dummy, $"preCompileStep_{stepName}");
            collection.SettingModels.Add(dummy);
        }
    }

    private async Task<(SettingsCollectionViewModel Collection, Dictionary<TitledSetting, string> Keys)>
        BuildCategoryCollectionAsync(string category)
    {
        var collection = new SettingsCollectionViewModel("") { ShowTitle = false };
        var keys = new Dictionary<TitledSetting, string>();

        foreach (var setting in _projectSettingsService.GetProjectSettingsList(category))
        {
            if (!setting.ActivationFunction(_root)) continue;

            TitledSetting? local;

            if (setting.HasFactory)
            {
                // Factory creates a fresh instance with the current project value already applied
                local = await setting.CreateSettingAsync(_root);
                if (local == null) continue;
            }
            else
            {
                local = setting.Setting!;

                // Pre-compile step checkboxes are backed by the shared preCompileSteps array
                if (local is CheckBoxSetting && setting.Key.StartsWith("preCompileStep_"))
                {
                    var stepName = setting.Key["preCompileStep_".Length..];
                    var enabledSteps = _root.Properties.GetStringArray("preCompileSteps")?.ToHashSet() ?? [];
                    local.Value = enabledSteps.Contains(stepName);
                }
                else if (_root.Properties.ContainsKey(setting.Key))
                {
                    var node = _root.Properties[setting.Key];
                    if (node == null) continue;

                    switch (local)
                    {
                        case CheckBoxSetting:
                            local.Value = node.ToString() == "True";
                            break;
                        case FolderPathSetting:
                        case FilePathSetting:
                        case TextBoxSetting:
                            local.Value = node.ToString();
                            break;
                        case ListBoxSetting:
                            local.Value = new ObservableCollection<string>(
                                node.AsArray().Select(n => n!.ToString()));
                            break;
                        case AdvancedComboBoxSearchSetting:
                        case AdvancedComboBoxSetting:
                        case ComboBoxSearchSetting:
                        case ComboBoxSetting:
                            local.Value = node.ToString();
                            break;
                        case SliderSetting:
                            local.Value = double.Parse(node.ToString(), CultureInfo.InvariantCulture);
                            break;
                        case ColorSetting:
                            Color.TryParse(node.ToString(), out var color);
                            local.Value = color;
                            break;
                        default:
                            _logger.Error($"Unknown setting of type: {local.GetType().Name}");
                            continue;
                    }
                }
                else
                {
                    local.Value = local.DefaultValue;
                }
            }

            local.Priority = setting.DisplayOrder;
            keys.Add(local, setting.Key);
            collection.SettingModels.Add(local);
        }

        return (collection, keys);
    }

    public async Task SaveAsync()
    {
        var enabledPreCompileSteps = new List<string>();

        foreach (var (_, (_, keys)) in _categoryData)
        foreach (var (setting, key) in keys)
        {
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
            ListBoxSetting lbs => new JsonArray(
                lbs.Items.Select(item => JsonValue.Create(
                    key is "include" or "exclude" ? item.ToUnixPath() : item)).ToArray()),
            _ => JsonValue.Create(setting.Value)
        };
    }

    public async Task SaveAndCloseAsync(FlexibleWindow window)
    {
        await SaveAsync();
        Close(window);
    }
}