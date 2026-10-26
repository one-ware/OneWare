using System.ComponentModel;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.FolderProjectSystem.Models;

namespace OneWare.FolderProjectSystem;

/// <summary>
/// Chat functions that open or create plain folder projects (a directory without a project file).
/// </summary>
public sealed class FolderProjectAiFunctions(
    FolderProjectManager manager,
    IProjectExplorerService projectExplorerService,
    IPaths paths)
{
    public void Register(IAiFunctionProvider provider)
    {
        foreach (var function in CreateFunctions()) provider.RegisterFunction(function);
    }

    internal IEnumerable<IOneWareAiFunction> CreateFunctions()
    {
        yield return new OneWareAiFunction
        {
            Name = "folder_open_project",
            FriendlyName = "Open folder",
            DetailExtractor = args => args.TryGetValue("path", out var path) ? path?.ToString() : null,
            Description = "Opens an existing directory as a folder project in the project explorer (like File > " +
                          "Open > Folder) and makes it the active project. Folder projects have no project file, " +
                          "no build settings and no Build/Debug actions; use a typed project (.swproj, .fpgaproj) " +
                          "when the user wants to build or debug.",
            Handler = (Func<string, Task<string>>)OpenAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "folder_create_project",
            FriendlyName = "Create folder",
            DetailExtractor = args => args.TryGetValue("name", out var name) ? name?.ToString() : null,
            Description = "Creates a new empty directory and opens it as a folder project, making it the active " +
                          "project.",
            Handler = (Func<string, string?, Task<string>>)CreateAsync,
            ConfirmationCheck = _ => "Create a new folder on disk and open it as a project?",
            RunOnUiThread = true
        };
    }

    private async Task<string> OpenAsync(
        [Description("Absolute path of the directory to open.")]
        string path)
    {
        try
        {
            var folder = path?.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "";

            if (string.IsNullOrEmpty(folder) || !Path.IsPathRooted(folder))
                return "Error: pass an absolute directory path.";
            if (!Directory.Exists(folder))
                return $"Error: the directory '{folder}' does not exist. Use folder_create_project to create it.";

            return await LoadAsync(folder);
        }
        catch (Exception e)
        {
            return $"Error: {e.Message}";
        }
    }

    private async Task<string> CreateAsync(
        [Description("Name of the new directory.")]
        string name,
        [Description("Existing parent directory. Defaults to the ONE WARE Studio projects directory.")]
        string? directory = null)
    {
        try
        {
            var folderName = name?.Trim() ?? "";
            if (folderName.Length == 0 || folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return $"Error: '{folderName}' is not a valid folder name.";

            var parent = string.IsNullOrWhiteSpace(directory) ? paths.ProjectsDirectory : directory.Trim();
            if (!Directory.Exists(parent)) return $"Error: the directory '{parent}' does not exist.";

            var folder = Path.Combine(parent, folderName);
            if (Directory.Exists(folder))
                return $"Error: '{folder}' already exists. Use folder_open_project to open it.";

            Directory.CreateDirectory(folder);
            return await LoadAsync(folder);
        }
        catch (Exception e)
        {
            return $"Error: {e.Message}";
        }
    }

    private async Task<string> LoadAsync(string folder)
    {
        // Loading the same folder twice would create a detached second root.
        if (projectExplorerService.Projects.FirstOrDefault(x => x.RootFolderPath.EqualPaths(folder)) is { } loaded)
        {
            projectExplorerService.ActiveProject = loaded;
            return $"'{folder}' was already open as '{loaded.Name}' and is now the active project.";
        }

        if (await projectExplorerService.LoadProjectAsync(folder, manager) is not FolderProjectRoot root)
            return $"Error: '{folder}' could not be opened.";

        projectExplorerService.ActiveProject = root;
        return $"Opened '{root.RootFolderPath}' as a folder project and made it active.";
    }
}
