using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Media;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;

namespace OneWare.UniversalFpgaProjectSystem.Services.Ai;

/// <summary>
/// Chat functions for the two kinds of settings an FPGA project has: the project settings that
/// plugins register (stored in the .fpgaproj file) and the device settings of the selected board
/// (stored in device-settings/&lt;board&gt;.deviceconf).
/// </summary>
public sealed class FpgaSettingsFunctions(
    FpgaAgentContext context,
    FpgaService fpgaService,
    UniversalFpgaProjectManager projectManager,
    IProjectSettingsService projectSettingsService)
{
    private const string PreCompileStepPrefix = "preCompileStep_";

    /// <summary>
    /// Creates the functions this group contributes to the chat.
    /// </summary>
    public IEnumerable<IOneWareAiFunction> CreateFunctions()
    {
        yield return new OneWareAiFunction
        {
            Name = "fpga_list_project_settings",
            FriendlyName = "List FPGA project settings",
            DetailExtractor = FpgaFunctionDetail.FirstOf("project settings", "category"),
            Description = "Lists the settings of the active FPGA project that plugins registered, with their " +
                          "key, category, current value and allowed values. These are the settings the project " +
                          "settings dialog shows. Use fpga_set_project_setting to change one.",
            Handler = (Func<string?, Task<string>>)ListProjectSettingsAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_project_setting",
            FriendlyName = "Set FPGA project setting",
            DetailExtractor = FpgaFunctionDetail.AllOf("project setting", "key", "value"),
            Description = "Changes one setting of the active FPGA project and saves the .fpgaproj file. Pass a " +
                          "key from fpga_list_project_settings. For a pre-compile step " +
                          "(key preCompileStep_<id>) pass true or false to enable or disable it.",
            Handler = (Func<string, string, Task<string>>)SetProjectSettingAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_list_device_settings",
            FriendlyName = "List FPGA device settings",
            DetailExtractor = FpgaFunctionDetail.Constant("device settings"),
            Description = "Lists the device settings of the board selected for the active project, read from " +
                          "device-settings/<board>.deviceconf. These describe the hardware itself, for example " +
                          "the device part number or the programming mode.",
            Handler = (Func<string>)ListDeviceSettings,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_device_setting",
            FriendlyName = "Set FPGA device setting",
            DetailExtractor = FpgaFunctionDetail.AllOf("device setting", "key", "value"),
            Description = "Sets one device setting of the board selected for the active project and writes the " +
                          ".deviceconf file. Pass an empty value to fall back to the board default.",
            Handler = (Func<string, string, string>)SetDeviceSetting,
            RunOnUiThread = true
        };
    }

    private Task<string> ListProjectSettingsAsync(
        [Description("Only list settings of this category. Omit to list every category.")]
        string? category = null)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();

            var allCategories = projectSettingsService.GetProjectCategories().ToList();

            List<string> categories;

            if (string.IsNullOrWhiteSpace(category))
            {
                categories = allCategories;
            }
            else
            {
                var requested = allCategories.FirstOrDefault(x =>
                                    string.Equals(x, category.Trim(), StringComparison.OrdinalIgnoreCase))
                                ?? throw new FpgaAgentException(
                                    $"'{category}' is not a settings category. Available: " +
                                    $"{string.Join(", ", allCategories)}.");

                categories = [requested];
            }

            var builder = new StringBuilder();
            var found = 0;

            foreach (var currentCategory in categories)
            {
                var settings = projectSettingsService.GetProjectSettingsList(currentCategory)
                    .Where(x => x.ActivationFunction(project))
                    .ToList();

                if (settings.Count == 0) continue;

                builder.AppendLine($"{currentCategory}:");

                foreach (var setting in settings)
                {
                    var model = setting.HasFactory
                        ? await setting.CreateSettingAsync(project)
                        : setting.Setting;

                    if (model == null) continue;

                    found++;
                    builder.AppendLine($"  - {setting.Key} ({DescribeType(model)})");
                    builder.AppendLine($"      Title: {model.Title}");
                    builder.AppendLine($"      Value: {DescribeValue(project, setting.Key, model)}");

                    if (DescribeOptions(model) is { } options)
                        builder.AppendLine($"      Allowed: {options}");
                }
            }

            if (found == 0)
                return string.IsNullOrWhiteSpace(category)
                    ? "The active project has no registered settings."
                    : $"No settings are registered in the category '{category}'. Available categories: " +
                      $"{string.Join(", ", projectSettingsService.GetProjectCategories())}.";

            return builder.ToString().TrimEnd();
        });
    }

    private Task<string> SetProjectSettingAsync(
        [Description("Key of the setting, as returned by fpga_list_project_settings.")]
        string key,
        [Description("The new value. For a checkbox pass true or false; for a list pass comma separated values.")]
        string value)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var settingKey = FpgaFunctionText.Require(key, nameof(key));

            if (settingKey.StartsWith(PreCompileStepPrefix, StringComparison.OrdinalIgnoreCase))
                return await SetPreCompileStepAsync(project, settingKey, value);

            var definition = projectSettingsService.GetProjectSettingsList()
                                 .FirstOrDefault(x => string.Equals(x.Key, settingKey, StringComparison.Ordinal))
                             ?? throw new FpgaAgentException(
                                 $"'{key}' is not a registered project setting. Call fpga_list_project_settings " +
                                 "for the available keys.");

            if (!definition.ActivationFunction(project))
                throw new FpgaAgentException(
                    $"The setting '{settingKey}' does not apply to '{project.Name}' with its current toolchain.");

            var model = definition.HasFactory
                ? await definition.CreateSettingAsync(project)
                : definition.Setting;

            if (model == null)
                throw new FpgaAgentException($"The setting '{settingKey}' could not be read.");

            project.Properties.SetNode(settingKey, ConvertValue(model, settingKey, value));

            await SaveAsync(project);
            await context.ProjectExplorerService.ReloadProjectAsync(project);

            return $"Set '{settingKey}' to '{value}' in '{project.Name}'.";
        });
    }

    private async Task<string> SetPreCompileStepAsync(UniversalFpgaProjectRoot project, string key, string value)
    {
        var requestedId = key[PreCompileStepPrefix.Length..];
        var enable = FpgaFunctionText.ParseBool(value, nameof(value));

        // The settings dialog compares the stored ids case-sensitively, so storing the caller's
        // spelling instead of the registered one would make it drop the entry on its next save.
        var stepId = fpgaService.PreCompileSteps
                         .FirstOrDefault(x => string.Equals(x.Id, requestedId, StringComparison.OrdinalIgnoreCase))
                         ?.Id
                     ?? throw new FpgaAgentException(
                         $"'{requestedId}' is not a registered pre-compile step. Available: " +
                         $"{string.Join(", ", fpgaService.PreCompileSteps.Select(x => x.Id))}.");

        var steps = (project.Properties.GetStringArray("preCompileSteps") ?? []).ToList();

        steps.RemoveAll(x => string.Equals(x, stepId, StringComparison.OrdinalIgnoreCase));

        if (enable) steps.Add(stepId);

        project.Properties.SetNode("preCompileSteps",
            new JsonArray(steps.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()));

        await SaveAsync(project);
        await context.ProjectExplorerService.ReloadProjectAsync(project);

        return $"{(enable ? "Enabled" : "Disabled")} the pre-compile step '{stepId}' for '{project.Name}'.";
    }

    private string ListDeviceSettings()
    {
        return FpgaFunctionGuard.Run(() =>
        {
            var project = context.RequireProject();
            var board = RequireBoard(project);

            var stored = FpgaSettingsParser.LoadSettings(project, board);
            var defaults = GetBoardDefaults(board);

            var keys = stored.Keys.Concat(defaults.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (keys.Count == 0)
                return $"The board '{board}' has no device settings.";

            var builder = new StringBuilder();
            builder.AppendLine($"Device settings of board '{board}':");

            foreach (var settingKey in keys)
            {
                var hasStored = stored.TryGetValue(settingKey, out var storedValue);
                var hasDefault = defaults.TryGetValue(settingKey, out var defaultValue);

                var origin = hasStored
                    ? hasDefault && storedValue == defaultValue ? "default" : "project"
                    : "default";

                builder.AppendLine(
                    $"  - {settingKey} = {(hasStored ? storedValue : defaultValue)} ({origin})");
            }

            return builder.ToString().TrimEnd();
        });
    }

    private string SetDeviceSetting(
        [Description("Key of the device setting, as returned by fpga_list_device_settings.")]
        string key,
        [Description("The new value. Pass an empty string to remove the entry and use the board default.")]
        string value)
    {
        return FpgaFunctionGuard.Run(() =>
        {
            var project = context.RequireProject();
            var board = RequireBoard(project);
            var settingKey = FpgaFunctionText.Require(key, nameof(key));

            var settings = FpgaSettingsParser.LoadSettings(project, board);
            var defaults = GetBoardDefaults(board);

            if (!settings.ContainsKey(settingKey) && !defaults.ContainsKey(settingKey))
                throw new FpgaAgentException(
                    $"'{settingKey}' is not a device setting of board '{board}'. Call " +
                    "fpga_list_device_settings for the available keys.");

            // An empty value is dropped by the parser, which is exactly the "use the default" case.
            settings[settingKey] = value;

            if (!FpgaSettingsParser.SaveSettings(project, board, settings))
                throw new FpgaAgentException($"The device settings of board '{board}' could not be written.");

            return string.IsNullOrEmpty(value)
                ? $"Removed '{settingKey}' from the device settings of '{board}', so its default applies again."
                : $"Set '{settingKey}' to '{value}' for board '{board}'.";
        });
    }

    private string RequireBoard(UniversalFpgaProjectRoot project)
    {
        if (string.IsNullOrEmpty(project.Board))
            throw new FpgaAgentException(
                "No board is selected. Call fpga_list_toolchains and fpga_set_board first.");

        return project.Board;
    }

    private IReadOnlyDictionary<string, string> GetBoardDefaults(string board)
    {
        var package = fpgaService.FpgaPackages.FirstOrDefault(x => x.Name == board);

        if (package == null) return new Dictionary<string, string>();

        try
        {
            return package.LoadFpga().Properties;
        }
        catch (Exception e)
        {
            throw new FpgaAgentException($"The board '{board}' could not be loaded: {e.Message}", e);
        }
    }

    private async Task SaveAsync(UniversalFpgaProjectRoot project)
    {
        if (!await projectManager.SaveProjectAsync(project))
            throw new FpgaAgentException($"'{project.Name}' could not be saved.");
    }

    private static string DescribeType(TitledSetting setting)
    {
        return setting switch
        {
            CheckBoxSetting => "boolean",
            SliderSetting => "number",
            ListBoxSetting => "list",
            ColorSetting => "color",
            FolderPathSetting => "folder path",
            FilePathSetting => "file path",
            ComboBoxSetting or AdvancedComboBoxSetting or ComboListBoxSetting => "choice",
            _ => "text"
        };
    }

    private static string DescribeValue(UniversalFpgaProjectRoot project, string key, TitledSetting setting)
    {
        // A pre-compile step has no property of its own; its state lives in the shared array.
        if (key.StartsWith(PreCompileStepPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var stepId = key[PreCompileStepPrefix.Length..];
            var enabled = (project.Properties.GetStringArray("preCompileSteps") ?? [])
                .Any(x => string.Equals(x, stepId, StringComparison.OrdinalIgnoreCase));

            return enabled.ToString();
        }

        if (project.Properties.GetNode(key) is { } node)
            return node is JsonArray array
                ? string.Join(", ", array.Select(x => x?.ToString()))
                : node.ToString();

        return $"{setting.DefaultValue} (default)";
    }

    private static string? DescribeOptions(TitledSetting setting)
    {
        return setting switch
        {
            CheckBoxSetting => "true, false",
            ComboBoxSetting combo => string.Join(", ", combo.Options.Select(x => x.ToString())),
            AdvancedComboBoxSetting advanced => string.Join(", ", advanced.Options.Select(x => x.Value.ToString())),
            ComboListBoxSetting comboList => string.Join(", ", comboList.Options),
            _ => null
        };
    }

    /// <summary>
    /// Converts a text argument into the node the project settings dialog would write for the same
    /// setting, so both writers stay interchangeable.
    /// </summary>
    private static JsonNode? ConvertValue(TitledSetting setting, string key, string value)
    {
        switch (setting)
        {
            case CheckBoxSetting:
                return JsonValue.Create(FpgaFunctionText.ParseBool(value, key));

            case SliderSetting:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    throw new FpgaAgentException($"'{value}' is not a number for the setting '{key}'.");
                return JsonValue.Create(number);

            case ColorSetting:
                if (!Color.TryParse(value, out _))
                    throw new FpgaAgentException($"'{value}' is not a color for the setting '{key}'.");
                return JsonValue.Create(value);

            case FolderPathSetting or FilePathSetting:
                return JsonValue.Create(value.ToUnixPath());

            case ListBoxSetting:
                return CreateArray(SplitItems(value), key);

            case ComboListBoxSetting comboList:
                return CreateArray(
                    SplitItems(value).Select(x => RequireOption(comboList.Options, x, key)), key);

            case ComboBoxSetting combo:
                return JsonValue.Create(RequireOption(combo.Options.Select(x => x.ToString()), value, key));

            case AdvancedComboBoxSetting advanced:
                return JsonValue.Create(
                    RequireOption(advanced.Options.Select(x => x.Value.ToString()), value, key));

            default:
                return JsonValue.Create(value);
        }
    }

    private static string[] SplitItems(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Builds a string array node. Path-like keys are normalized to forward slashes, because the
    /// glob matching compares patterns verbatim and would never match a Windows style pattern.
    /// </summary>
    private static JsonArray CreateArray(IEnumerable<string> items, string key)
    {
        var normalize = key is "include" or "exclude" or "compileExcluded" or "testBenches";

        return new JsonArray(items
            .Select(x => (JsonNode?)JsonValue.Create(normalize ? x.ToUnixPath() : x))
            .ToArray());
    }

    /// <summary>
    /// Returns the allowed option that matches <paramref name="value"/>, so the stored value keeps
    /// the exact casing the setting declares.
    /// </summary>
    private static string RequireOption(IEnumerable<string?> options, string value, string key)
    {
        var allowed = options.Where(x => x != null).Select(x => x!).ToList();

        return allowed.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))
               ?? throw new FpgaAgentException(
                   $"'{value}' is not allowed for the setting '{key}'. Allowed: {string.Join(", ", allowed)}.");
    }
}
