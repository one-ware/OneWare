using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services.Ai;

/// <summary>
/// Raised when an FPGA chat function cannot do its work, for example because no FPGA project is
/// active or an argument does not match anything in the project.
/// </summary>
public sealed class FpgaAgentException : Exception
{
    public FpgaAgentException(string message) : base(message)
    {
    }

    public FpgaAgentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Gives the FPGA chat functions access to the project the user currently works on.
/// </summary>
/// <remarks>
/// The chat has no notion of the project explorer selection, so every function resolves the active
/// project through this single entry point in order to produce one consistent error message when
/// there is none.
/// </remarks>
public sealed class FpgaAgentContext(IProjectExplorerService projectExplorerService)
{
    private const string NoProjectMessage =
        "No FPGA project is active. Call fpga_list_projects and fpga_set_active_project, or create " +
        "one with fpga_create_project.";

    public IProjectExplorerService ProjectExplorerService { get; } = projectExplorerService;

    /// <summary>
    /// Returns the active FPGA project.
    /// </summary>
    /// <exception cref="FpgaAgentException">No FPGA project is active.</exception>
    public UniversalFpgaProjectRoot RequireProject()
    {
        if (ProjectExplorerService.ActiveProject is not UniversalFpgaProjectRoot project)
            throw new FpgaAgentException(NoProjectMessage);

        return project;
    }

    /// <summary>
    /// Returns every loaded FPGA project.
    /// </summary>
    public IReadOnlyList<UniversalFpgaProjectRoot> GetProjects()
    {
        return ProjectExplorerService.Projects.OfType<UniversalFpgaProjectRoot>().ToList();
    }

    /// <summary>
    /// Resolves a loaded FPGA project by name or by (partial) project file path.
    /// </summary>
    /// <exception cref="FpgaAgentException">Nothing matched, or the name was ambiguous.</exception>
    public UniversalFpgaProjectRoot ResolveProject(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
            throw new FpgaAgentException("Pass the name or the .fpgaproj path of the project.");

        var projects = GetProjects();

        if (projects.Count == 0)
            throw new FpgaAgentException("No FPGA project is loaded. Create one with fpga_create_project.");

        var needle = nameOrPath.Trim();

        var matches = projects
            .Where(x => string.Equals(x.Name, needle, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.ProjectFilePath, needle, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.RootFolderPath, needle, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            matches = projects
                .Where(x => x.ProjectFilePath.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new FpgaAgentException(
                $"No loaded FPGA project matches '{nameOrPath}'. Loaded projects: " +
                $"{string.Join(", ", projects.Select(x => x.Name))}."),
            _ => throw new FpgaAgentException(
                $"'{nameOrPath}' matches several projects: " +
                $"{string.Join(", ", matches.Select(x => x.ProjectFilePath))}. Pass the full path.")
        };
    }

    /// <summary>
    /// Resolves a project file from a relative or absolute path.
    /// </summary>
    /// <exception cref="FpgaAgentException">The file is not part of the project.</exception>
    public IProjectFile RequireFile(UniversalFpgaProjectRoot project, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new FpgaAgentException("Pass the path of a file that belongs to the project.");

        var relative = ToRelativePath(project, path);

        if (project.GetFile(relative) is { } file) return file;

        throw new FpgaAgentException(
            $"'{path}' is not a file of project '{project.Name}'. Call fpga_list_files to see the " +
            "exact relative paths.");
    }

    /// <summary>
    /// Converts an absolute path into a project relative one, and normalizes the separators.
    /// </summary>
    public static string ToRelativePath(UniversalFpgaProjectRoot project, string path)
    {
        var trimmed = path.Trim();

        if (Path.IsPathRooted(trimmed))
            trimmed = Path.GetRelativePath(project.RootFolderPath, trimmed);

        return trimmed.Replace('\\', '/');
    }
}
