using OneWare.Essentials.Services;
using OneWare.SoftwareProjectSystem.Models;
using OneWare.SoftwareProjectSystem.Templates;

namespace OneWare.SoftwareProjectSystem.Services;

/// <summary>
/// Creates <c>.swproj</c> projects on disk and adds them to the project explorer. Shared by the creator dialog and
/// the chat functions.
/// </summary>
public class SoftwareProjectFactory(
    SoftwareProjectManager manager,
    ToolchainKitService kitService,
    IProjectExplorerService projectExplorerService)
{
    /// <summary>
    /// Writes the project file and template files, loads the project and makes it active.
    /// </summary>
    /// <param name="name">Project name, used for the .swproj file (and the folder when <paramref name="createFolder" />).</param>
    /// <param name="location">Existing directory the project (folder) is created in.</param>
    /// <param name="createFolder">Create a sub-folder named <paramref name="name" /> inside <paramref name="location" />.</param>
    /// <param name="template">Template from <see cref="SoftwareProjectTemplates.All" />, or null for an empty project.</param>
    /// <param name="kit">Toolchain kit, or null to use the first host compiler at build time.</param>
    /// <exception cref="SoftwareProjectCreationException">The arguments are invalid or the project already exists.</exception>
    public async Task<SoftwareProjectCreationResult> CreateAsync(string name, string location, bool createFolder,
        SoftwareProjectTemplate? template, ToolchainKit? kit)
    {
        name = name.Trim();
        location = location.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new SoftwareProjectCreationException($"'{name}' is not a valid project name.");

        if (string.IsNullOrWhiteSpace(location) || !Directory.Exists(location))
            throw new SoftwareProjectCreationException($"The directory '{location}' does not exist.");

        var folder = createFolder ? Path.Combine(location, name) : location;
        var projectFile = Path.Combine(folder, name + SoftwareProjectRoot.ProjectFileExtension);

        if (File.Exists(projectFile))
            throw new SoftwareProjectCreationException($"'{projectFile}' already exists.");

        Directory.CreateDirectory(folder);

        var createdFiles = new List<string>();
        if (template != null)
            foreach (var (relativePath, content) in template.CreateFiles(name))
            {
                var path = Path.Combine(folder, relativePath);
                if (File.Exists(path)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, content);
                createdFiles.Add(path);
            }

        var root = new SoftwareProjectRoot(projectFile);
        root.Properties.AddToStringArray("include", "*");
        root.Properties.AddToStringArray("exclude", "build", ".git");
        root.BuildSystem = template?.BuildSystem ?? SoftwareProjectRoot.BuildSystemSimple;
        root.Configuration = SoftwareProjectRoot.ConfigurationDebug;
        if (template?.CxxStandard != null) root.CxxStandard = template.CxxStandard;
        if (template?.CStandard != null) root.CStandard = template.CStandard;
        if (template?.BuildSystem == SoftwareProjectRoot.BuildSystemCMake)
            root.OutputName = SoftwareProjectTemplates.SanitizeTarget(name);
        if (kit != null) root.Kit = kit.Id;

        if (!await manager.SaveProjectAsync(root))
            throw new SoftwareProjectCreationException($"Could not write '{projectFile}'.");

        await root.InitializeAsync();
        projectExplorerService.AddProject(root);
        projectExplorerService.ActiveProject = root;
        root.IsExpanded = true;

        var mainSource = createdFiles.FirstOrDefault(x => Path.GetExtension(x) is ".c" or ".cpp");
        return new SoftwareProjectCreationResult(root, createdFiles, mainSource);
    }

    /// <summary>
    /// Finds a template by name (case-insensitive). <see cref="SoftwareProjectTemplates.Empty" /> and empty input
    /// return null.
    /// </summary>
    public static SoftwareProjectTemplate? ResolveTemplate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.Equals(name.Trim(), SoftwareProjectTemplates.Empty, StringComparison.OrdinalIgnoreCase))
            return null;

        return SoftwareProjectTemplates.All.FirstOrDefault(x =>
                   string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? throw new SoftwareProjectCreationException(
                   $"'{name}' is not a project template. Available: {SoftwareProjectTemplates.Empty}, " +
                   $"{string.Join(", ", SoftwareProjectTemplates.All.Select(x => x.Name))}.");
    }

    /// <summary>
    /// Finds a kit by id or name (case-insensitive). Empty input returns null (auto).
    /// </summary>
    public ToolchainKit? ResolveKit(string? idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName)) return null;

        var needle = idOrName.Trim();
        return kitService.Kits.FirstOrDefault(x =>
                   string.Equals(x.Id, needle, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(x.Name, needle, StringComparison.OrdinalIgnoreCase))
               ?? throw new SoftwareProjectCreationException(
                   $"'{idOrName}' is not an available toolchain kit. Available: " +
                   $"{(kitService.Kits.Count == 0 ? "none" : string.Join(", ", kitService.Kits.Select(x => x.Id)))}.");
    }
}

public sealed record SoftwareProjectCreationResult(
    SoftwareProjectRoot Root,
    IReadOnlyList<string> CreatedFiles,
    string? MainSourceFile);

public sealed class SoftwareProjectCreationException(string message) : Exception(message);
