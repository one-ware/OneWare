using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.SoftwareProjectSystem.Models;
using OneWare.SoftwareProjectSystem.Services;

namespace OneWare.SoftwareProjectSystem.ViewModels;

/// <summary>
/// Settings editor for <c>.swproj</c> files, laid out like the FPGA project settings: categories on the left,
/// settings of the selected category on the right.
/// </summary>
public class SoftwareProjectSettingsViewModel : FlexibleWindowViewModelBase
{
    private const string AutoKit = "Auto (first host compiler)";

    private readonly Dictionary<string, SettingsCollectionViewModel> _categories = new();
    private readonly ComboBoxSetting _buildSystem;
    private readonly TextBoxSetting _cmakeArguments;
    private readonly ListBoxSetting _compileExcluded;
    private readonly ListBoxSetting _compilerFlags;
    private readonly ComboBoxSetting _configuration;
    private readonly TextBoxSetting _cStandard;
    private readonly TextBoxSetting _cxxStandard;
    private readonly ListBoxSetting _defines;
    private readonly ListBoxSetting _exclude;
    private readonly ListBoxSetting _include;
    private readonly ListBoxSetting _includeDirectories;
    private readonly ComboBoxSetting _kit;
    private readonly ListBoxSetting _libraries;
    private readonly ListBoxSetting _linkerFlags;
    private readonly ILogger _logger;
    private readonly TextBoxSetting _output;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly TextBoxSetting _runArguments;

    private string? _selectedCategory;
    private SettingsCollectionViewModel? _settingsCollection;

    public SoftwareProjectSettingsViewModel(SoftwareProjectRoot root, ILogger logger,
        IProjectExplorerService projectExplorerService, ToolchainKitService kitService)
    {
        Root = root;
        _logger = logger;
        _projectExplorerService = projectExplorerService;
        Title = $"{root.Header} Settings";

        kitService.Refresh();
        var kits = new[] { AutoKit }.Concat(kitService.Kits.Select(x => x.Id)).ToList();
        if (root.Kit != null && !kits.Contains(root.Kit)) kits.Add(root.Kit);

        _buildSystem = new ComboBoxSetting("Build System", root.BuildSystem,
            [SoftwareProjectRoot.BuildSystemSimple, SoftwareProjectRoot.BuildSystemCMake])
        {
            MarkdownDocumentation =
                "How the project is built.\n\n" +
                "- `simple`: OneWare calls the compiler for every source file in the project (incremental, parallel)\n" +
                "- `cmake`: uses the `CMakeLists.txt` in the project root"
        };
        _kit = new ComboBoxSetting("Toolchain", root.Kit ?? AutoKit, kits.ToArray<object>())
        {
            MarkdownDocumentation =
                "Compiler kit used to build the project.\n\n" +
                "Host kits build programs for this machine (Run and Debug available), cross kits such as " +
                "`arm-none-eabi` build firmware."
        };
        _configuration = new ComboBoxSetting("Configuration", root.Configuration,
            SoftwareProjectRoot.Configurations.ToArray<object>())
        {
            MarkdownDocumentation =
                "`Debug` builds without optimization and with debug info (`-g -O0`), `Release` with `-O2 -DNDEBUG`.\n\n" +
                "Each configuration has its own folder in `build/`."
        };
        _output = new TextBoxSetting("Output Name", root.HasExplicitOutputName ? root.OutputName : "",
            root.OutputName)
        {
            MarkdownDocumentation =
                "Name of the produced program (simple build system) or of the CMake target to run and debug.\n\n" +
                "Defaults to the project name."
        };

        _cStandard = new TextBoxSetting("C Standard", root.CStandard ?? "", "e.g. c17")
        {
            MarkdownDocumentation = "Passed as `-std=...` when compiling C files, e.g. `c11`, `c17`, `gnu17`."
        };
        _cxxStandard = new TextBoxSetting("C++ Standard", root.CxxStandard ?? "", "e.g. c++20")
        {
            MarkdownDocumentation = "Passed as `-std=...` when compiling C++ files, e.g. `c++17`, `c++20`, `gnu++23`."
        };
        _includeDirectories = new ListBoxSetting("Include Directories", root.IncludeDirectories.ToArray())
        {
            MarkdownDocumentation = "Directories searched for headers (`-I`), relative to the project root."
        };
        _defines = new ListBoxSetting("Defines", root.Defines.ToArray())
        {
            MarkdownDocumentation = "Preprocessor definitions (`-D`), e.g. `DEBUG_LOG` or `VERSION=2`."
        };
        _compilerFlags = new ListBoxSetting("Compiler Flags", root.CompilerFlags.ToArray())
        {
            MarkdownDocumentation = "Extra flags for every compile command, e.g. `-Wall`, `-Wextra` or `-mcpu=cortex-m4`."
        };

        _linkerFlags = new ListBoxSetting("Linker Flags", root.LinkerFlags.ToArray())
        {
            MarkdownDocumentation = "Extra flags for the link command, e.g. `-T linker.ld` or `-static`."
        };
        _libraries = new ListBoxSetting("Libraries", root.Libraries.ToArray())
        {
            MarkdownDocumentation = "Libraries to link (`-l`), e.g. `m` or `pthread`."
        };

        _cmakeArguments = new TextBoxSetting("CMake Arguments", string.Join(' ', root.CMakeArguments),
            "-DOPTION=ON")
        {
            MarkdownDocumentation =
                "Extra arguments for the CMake configure step, separated by spaces.\n\n" +
                "Changing them reconfigures the build folder from scratch."
        };

        _runArguments = new TextBoxSetting("Run Arguments", root.RunArguments ?? "", "")
        {
            MarkdownDocumentation = "Command line arguments passed to the program on Run and Debug."
        };

        _include = new ListBoxSetting("Files to Include", GetArray("include"))
        {
            MarkdownDocumentation =
                "Glob patterns or relative paths that are **explicitly included** in the project file set.\n\n" +
                "`*` includes every file in the project folder."
        };
        _exclude = new ListBoxSetting("Files to Exclude", GetArray("exclude"))
        {
            MarkdownDocumentation =
                "Glob patterns or relative paths that are **excluded** from the project file set.\n\n" +
                "Excluded files are hidden from the project explorer and never compiled."
        };
        _compileExcluded = new ListBoxSetting("Build Excluded", GetArray("compileExcluded"))
        {
            MarkdownDocumentation =
                "Relative paths or patterns of source files that are **excluded from the build**.\n\n" +
                "The files stay visible in the project explorer but the simple build system skips them."
        };

        AddCategory("Project", _buildSystem, _kit, _configuration, _output);
        AddCategory("Compiler", _cStandard, _cxxStandard, _includeDirectories, _defines, _compilerFlags);
        AddCategory("Linker", _linkerFlags, _libraries);
        AddCategory("CMake", _cmakeArguments);
        AddCategory("Run", _runArguments);
        AddCategory("Files", _include, _exclude, _compileExcluded);

        SelectedCategory = SettingCategories.FirstOrDefault();
    }

    public SoftwareProjectRoot Root { get; }

    public ObservableCollection<string> SettingCategories { get; } = [];

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
            if (value != null && _categories.TryGetValue(value, out var collection))
                SettingsCollection = collection;
        }
    }

    public async Task SaveAndCloseAsync(FlexibleWindow window)
    {
        try
        {
            Root.BuildSystem = _buildSystem.Value.ToString() ?? SoftwareProjectRoot.BuildSystemSimple;
            var kit = _kit.Value.ToString();
            Root.Kit = kit == AutoKit ? null : kit;
            Root.Configuration = _configuration.Value.ToString() ?? SoftwareProjectRoot.ConfigurationDebug;
            Root.SetText("output", Text(_output));
            Root.RunArguments = Text(_runArguments);
            Root.CStandard = Text(_cStandard);
            Root.CxxStandard = Text(_cxxStandard);
            Root.SetList("includeDirectories", _includeDirectories.Items);
            Root.SetList("defines", _defines.Items);
            Root.SetList("compilerFlags", _compilerFlags.Items);
            Root.SetList("linkerFlags", _linkerFlags.Items);
            Root.SetList("libraries", _libraries.Items);
            Root.SetList("cmakeArguments",
                Text(_cmakeArguments)?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? []);

            // An empty include list would hide every file, so it is kept as an empty array on purpose.
            Root.Properties.SetNode("include", ToArray(_include.Items));
            Root.SetList("exclude", _exclude.Items.Select(x => x.ToUnixPath()));
            Root.SetList("compileExcluded", _compileExcluded.Items.Select(x => x.ToUnixPath()));

            await _projectExplorerService.SaveProjectAsync(Root);

            // Include / exclude changes need the file tree to be rebuilt.
            await _projectExplorerService.ReloadProjectAsync(Root);

            Close(window);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    private void AddCategory(string name, params TitledSetting[] settings)
    {
        var collection = new SettingsCollectionViewModel("") { ShowTitle = false };
        foreach (var setting in settings) collection.SettingModels.Add(setting);
        _categories[name] = collection;
        SettingCategories.Add(name);
    }

    private string[] GetArray(string key)
    {
        return Root.Properties.GetStringArray(key)?.ToArray() ?? [];
    }

    private static System.Text.Json.Nodes.JsonArray ToArray(IEnumerable<string> items)
    {
        return new System.Text.Json.Nodes.JsonArray(items.Select(x => x.Trim()).Where(x => x.Length > 0)
            .Select(x => (System.Text.Json.Nodes.JsonNode?)System.Text.Json.Nodes.JsonValue.Create(x.ToUnixPath()))
            .ToArray());
    }

    private static string? Text(TextBoxSetting setting)
    {
        var text = setting.Value.ToString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
