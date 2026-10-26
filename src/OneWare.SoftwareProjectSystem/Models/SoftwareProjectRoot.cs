using System.Text.Json.Nodes;
using OneWare.Essentials.Models;
using OneWare.ProjectSystem.Models;

namespace OneWare.SoftwareProjectSystem.Models;

/// <summary>
/// A C/C++ software project (<c>.swproj</c>): host programs, microcontroller or softcore firmware.
/// </summary>
public class SoftwareProjectRoot : UniversalProjectRoot
{
    public const string ProjectFileExtension = ".swproj";
    public const string ProjectType = "SoftwareProject";

    public const string BuildSystemSimple = "simple";
    public const string BuildSystemCMake = "cmake";

    public const string ConfigurationDebug = "Debug";
    public const string ConfigurationRelease = "Release";

    public static readonly string[] Configurations = [ConfigurationDebug, ConfigurationRelease];

    public SoftwareProjectRoot(string projectFilePath) : base(projectFilePath)
    {
        Icon = new IconModel("SoftwareProject");

        RegisterProjectEntryModification(x =>
            x.TextOpacity = x is IProjectFile file && IsCompileExcluded(file.RelativePath) ? 0.5f : 1.0f);
    }

    public override string ProjectTypeId => ProjectType;

    /// <summary>
    /// True if the file is listed in <c>compileExcluded</c>: it stays in the project but the simple build skips it.
    /// </summary>
    public bool IsCompileExcluded(string relativePath)
    {
        return Properties.IsIncludedPathHelper(relativePath, "compileExcluded");
    }

    public void AddCompileExcluded(string relativePath)
    {
        Properties.AddIncludedPathHelper(relativePath, "compileExcluded");
        InvalidateAllModifications();
    }

    public void RemoveCompileExcluded(string relativePath)
    {
        Properties.RemoveIncludedPathHelper(relativePath, "compileExcluded");
        InvalidateAllModifications();
    }

    /// <summary>
    /// <c>simple</c> (OneWare drives the compiler directly) or <c>cmake</c> (an existing CMakeLists.txt).
    /// Stored as <c>buildSystem</c>.
    /// </summary>
    public string BuildSystem
    {
        get => Properties.GetString("buildSystem") ?? BuildSystemSimple;
        set => SetText("buildSystem", value);
    }

    /// <summary>
    /// Id of the <see cref="Services.ToolchainKit" /> used to build. Stored as <c>kit</c>.
    /// </summary>
    public string? Kit
    {
        get => Properties.GetString("kit");
        set => SetText("kit", value);
    }

    /// <summary>
    /// <c>Debug</c> or <c>Release</c>. Stored as <c>configuration</c>.
    /// </summary>
    public string Configuration
    {
        get => Properties.GetString("configuration") is { Length: > 0 } configuration
            ? configuration
            : ConfigurationDebug;
        set => SetText("configuration", value);
    }

    /// <summary>
    /// Name of the produced program (simple builds) or of the CMake target to run/debug. Defaults to the
    /// project name. Stored as <c>output</c>.
    /// </summary>
    public string OutputName
    {
        get => Properties.GetString("output") is { Length: > 0 } output
            ? output
            : Path.GetFileNameWithoutExtension(ProjectFilePath);
        set => SetText("output", value);
    }

    public bool HasExplicitOutputName => Properties.GetString("output") is { Length: > 0 };

    /// <summary>
    /// C standard for simple builds, e.g. <c>c17</c>. Stored as <c>cStandard</c>.
    /// </summary>
    public string? CStandard
    {
        get => Properties.GetString("cStandard");
        set => SetText("cStandard", value);
    }

    /// <summary>
    /// C++ standard for simple builds, e.g. <c>c++20</c>. Stored as <c>cxxStandard</c>.
    /// </summary>
    public string? CxxStandard
    {
        get => Properties.GetString("cxxStandard");
        set => SetText("cxxStandard", value);
    }

    /// <summary>
    /// Command line arguments passed to the program on Run and Debug. Stored as <c>runArguments</c>.
    /// </summary>
    public string? RunArguments
    {
        get => Properties.GetString("runArguments");
        set => SetText("runArguments", value);
    }

    public IReadOnlyList<string> IncludeDirectories => GetList("includeDirectories");
    public IReadOnlyList<string> Defines => GetList("defines");
    public IReadOnlyList<string> CompilerFlags => GetList("compilerFlags");
    public IReadOnlyList<string> LinkerFlags => GetList("linkerFlags");
    public IReadOnlyList<string> Libraries => GetList("libraries");
    public IReadOnlyList<string> CMakeArguments => GetList("cmakeArguments");

    /// <summary>
    /// Root of all build output; the configuration gets its own sub folder.
    /// </summary>
    public string BuildDirectory => Path.Combine(RootFolderPath, "build");

    public string ConfigurationBuildDirectory => Path.Combine(BuildDirectory, Configuration);

    /// <summary>
    /// Source files (relative to the root) that the simple build compiles: every included C/C++/assembly
    /// file outside the build folder.
    /// </summary>
    public IReadOnlyList<string> GetSourceFiles()
    {
        return GetFiles()
            .Where(Build.SimpleBuildSystem.IsSource)
            .Where(x => !IsInBuildFolder(x) && !IsCompileExcluded(x))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Sets a string property, or removes it if <paramref name="value" /> is empty.
    /// </summary>
    public void SetText(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            Properties.RemoveValue(key, out _);
        else
            Properties.SetString(key, value);
    }

    public void SetList(string key, IEnumerable<string> values)
    {
        var items = values.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        if (items.Length == 0)
            Properties.RemoveValue(key, out _);
        else
            Properties.SetNode(key, new JsonArray(items.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()));
    }

    private static bool IsInBuildFolder(string relativePath)
    {
        var first = relativePath.Replace('\\', '/').Split('/')[0];
        return first.Equals("build", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> GetList(string key)
    {
        return Properties.GetStringArray(key)?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];
    }
}
