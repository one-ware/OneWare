using System.ComponentModel;
using System.Text;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services.Ai;

/// <summary>
/// Chat functions that create, open and describe FPGA projects.
/// </summary>
public sealed class FpgaProjectFunctions(
    FpgaAgentContext context,
    FpgaService fpgaService,
    UniversalFpgaProjectManager projectManager,
    IPaths paths)
{
    /// <summary>
    /// Creates the functions this group contributes to the chat.
    /// </summary>
    public IEnumerable<IOneWareAiFunction> CreateFunctions()
    {
        yield return new OneWareAiFunction
        {
            Name = "fpga_list_projects",
            FriendlyName = "List FPGA projects",
            DetailExtractor = FpgaFunctionDetail.Constant("loaded projects"),
            Description = "Lists every FPGA project loaded in the IDE and marks the active one. Every other " +
                          "fpga_* function acts on the active project, so call this first when unsure which " +
                          "project is in scope.",
            Handler = (Func<string>)ListProjects,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_active_project",
            FriendlyName = "Select FPGA project",
            DetailExtractor = FpgaFunctionDetail.FirstOf("project", "project"),
            Description = "Makes a loaded FPGA project the active one, so the following fpga_* calls act on it. " +
                          "Pass the project name or the path of its .fpgaproj file, as returned by " +
                          "fpga_list_projects.",
            Handler = (Func<string, string>)SetActiveProject,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_get_project_overview",
            FriendlyName = "Read FPGA project",
            DetailExtractor = FpgaFunctionDetail.Constant("project overview"),
            Description = "Returns a snapshot of the active FPGA project: paths, selected board, toolchain, " +
                          "loader, top entity, enabled pre-compile steps, test benches and files excluded from " +
                          "compilation. Call this before changing anything.",
            Handler = (Func<string>)GetOverview,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_create_project",
            FriendlyName = "Create FPGA project",
            DetailExtractor = FpgaFunctionDetail.AllOf("new project", "name", "toolchain"),
            Description = "Creates a new FPGA project (.fpgaproj) in its own folder, loads it and makes it " +
                          "active. Use fpga_list_toolchains first to get valid toolchain, loader and template " +
                          "ids. Select the board afterwards with fpga_set_board.",
            Handler = (Func<string, string?, string?, string?, string?, Task<string>>)CreateProjectAsync,
            ConfirmationCheck = _ => "Create a new FPGA project? This creates a folder and a .fpgaproj file on disk.",
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_open_project",
            FriendlyName = "Open FPGA project",
            DetailExtractor = FpgaFunctionDetail.FirstOf("project file", "path"),
            Description = "Loads an existing .fpgaproj file from disk into the IDE and makes it the active " +
                          "project. Pass the absolute path of the .fpgaproj file.",
            Handler = (Func<string, Task<string>>)OpenProjectAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_save_project",
            FriendlyName = "Save FPGA project",
            DetailExtractor = FpgaFunctionDetail.Constant("save project"),
            Description = "Writes the pending changes of the active FPGA project to its .fpgaproj file. The " +
                          "other fpga_* functions already save, so this is only needed after a manual edit.",
            Handler = (Func<Task<string>>)SaveProjectAsync,
            RunOnUiThread = true
        };
    }

    private string ListProjects()
    {
        return FpgaFunctionGuard.Run(() =>
        {
            var projects = context.GetProjects();

            if (projects.Count == 0)
                return "No FPGA project is loaded. Use fpga_create_project or fpga_open_project.";

            var active = context.ProjectExplorerService.ActiveProject;
            var builder = new StringBuilder();

            builder.AppendLine($"{projects.Count} FPGA project(s) loaded:");

            foreach (var project in projects)
                builder.AppendLine(
                    $"  - {project.Name}{(ReferenceEquals(project, active) ? " (active)" : "")} — {project.ProjectFilePath}");

            return builder.ToString().TrimEnd();
        });
    }

    private string SetActiveProject(
        [Description("Name of the project, or the path of its .fpgaproj file.")]
        string project)
    {
        return FpgaFunctionGuard.Run(() =>
        {
            var resolved = context.ResolveProject(project);
            context.ProjectExplorerService.ActiveProject = resolved;

            return $"'{resolved.Name}' is now the active project ({resolved.ProjectFilePath}).";
        });
    }

    private string GetOverview()
    {
        return FpgaFunctionGuard.Run(() =>
        {
            var project = context.RequireProject();
            var builder = new StringBuilder();

            FpgaFunctionText.AppendField(builder, "Name", project.Name);
            FpgaFunctionText.AppendField(builder, "ProjectFile", project.ProjectFilePath);
            FpgaFunctionText.AppendField(builder, "RootFolder", project.RootFolderPath);
            FpgaFunctionText.AppendField(builder, "Board", project.Board);
            FpgaFunctionText.AppendField(builder, "Toolchain", DescribeToolchain(project.Toolchain));
            FpgaFunctionText.AppendField(builder, "Loader", DescribeLoader(project.Loader));
            FpgaFunctionText.AppendField(builder, "TopEntity", project.TopEntity);

            FpgaFunctionText.AppendList(builder, "PreCompileSteps",
                project.Properties.GetStringArray("preCompileSteps") ?? []);
            FpgaFunctionText.AppendList(builder, "TestBenches",
                project.Properties.GetStringArray("testBenches") ?? []);
            FpgaFunctionText.AppendList(builder, "CompileExcluded",
                project.Properties.GetStringArray("compileExcluded") ?? []);

            return builder.ToString().TrimEnd();
        });
    }

    private Task<string> CreateProjectAsync(
        [Description("Name of the project. Used for the folder and the .fpgaproj file.")]
        string name,
        [Description("Directory the project folder is created in. Defaults to the ONE WARE Studio projects directory.")]
        string? directory = null,
        [Description("Id of the toolchain to use, as returned by fpga_list_toolchains.")]
        string? toolchain = null,
        [Description("Id of the loader to use, as returned by fpga_list_toolchains.")]
        string? loader = null,
        [Description("Name of the project template to fill the project with, as returned by fpga_list_toolchains.")]
        string? template = null)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var projectName = FpgaFunctionText.Require(name, nameof(name));

            if (projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new FpgaAgentException($"'{projectName}' is not a valid project name.");

            var parentFolder = string.IsNullOrWhiteSpace(directory) ? paths.ProjectsDirectory : directory.Trim();

            if (!Directory.Exists(parentFolder))
                throw new FpgaAgentException($"The directory '{parentFolder}' does not exist.");

            var folder = Path.Combine(parentFolder, projectName);
            var projectFile = Path.Combine(folder, projectName + UniversalFpgaProjectRoot.ProjectFileExtension);

            if (File.Exists(projectFile))
                throw new FpgaAgentException($"'{projectFile}' already exists. Open it with fpga_open_project.");

            if (context.GetProjects().Any(x => x.RootFolderPath.EqualPaths(folder)))
                throw new FpgaAgentException(
                    $"Another FPGA project is already loaded from '{folder}'. A folder holds one project.");

            // Resolve every id before touching the disk, so an invalid argument does not leave an
            // empty folder or a half-configured project behind.
            var selectedLoader = string.IsNullOrWhiteSpace(loader) ? null : ResolveLoader(loader);
            var selectedToolchain = string.IsNullOrWhiteSpace(toolchain) ? null : ResolveToolchain(toolchain);
            var selectedTemplate = string.IsNullOrWhiteSpace(template) ? null : ResolveTemplate(template);

            Directory.CreateDirectory(folder);

            var root = new UniversalFpgaProjectRoot(projectFile);
            root.Properties.AddToStringArray("include", ["*.vhd", "*.vhdl", "*.v", "*.vcd", "vhdl_ls.toml"]);
            root.Properties.AddToStringArray("exclude", ["build"]);

            if (selectedLoader != null) root.Loader = selectedLoader.Id;

            if (selectedToolchain != null)
            {
                root.Toolchain = selectedToolchain.Id;
                selectedToolchain.OnProjectCreated(root);
            }

            await SaveAsync(root);

            context.ProjectExplorerService.AddProject(root);

            if (!context.ProjectExplorerService.Projects.Contains(root))
                throw new FpgaAgentException(
                    $"'{projectFile}' was written but could not be added to the project explorer.");

            context.ProjectExplorerService.ActiveProject = root;

            selectedTemplate?.FillTemplate(root);

            await SaveAsync(root);

            root.IsExpanded = true;

            var builder = new StringBuilder();
            builder.AppendLine($"Created and activated the FPGA project '{projectName}'.");
            FpgaFunctionText.AppendField(builder, "ProjectFile", projectFile);
            FpgaFunctionText.AppendField(builder, "Toolchain", DescribeToolchain(root.Toolchain));
            FpgaFunctionText.AppendField(builder, "Loader", DescribeLoader(root.Loader));
            FpgaFunctionText.AppendField(builder, "Template", selectedTemplate?.Name);
            builder.Append("Select the board next with fpga_set_board.");

            return builder.ToString();
        });
    }

    private Task<string> OpenProjectAsync(
        [Description("Absolute path of the .fpgaproj file to load.")]
        string path)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var projectFile = FpgaFunctionText.Require(path, nameof(path));

            if (!File.Exists(projectFile))
                throw new FpgaAgentException($"'{projectFile}' does not exist.");

            if (!projectFile.EndsWith(UniversalFpgaProjectRoot.ProjectFileExtension, StringComparison.OrdinalIgnoreCase))
                throw new FpgaAgentException(
                    $"'{projectFile}' is not an FPGA project file ({UniversalFpgaProjectRoot.ProjectFileExtension}).");

            // Loading an already loaded project would build a second root that the explorer rejects
            // but that still becomes the active one, so every later change would go to a detached
            // instance and be lost on the next save from the UI.
            if (context.GetProjects().FirstOrDefault(x => x.ProjectFilePath.EqualPaths(projectFile)) is { } loadedRoot)
            {
                context.ProjectExplorerService.ActiveProject = loadedRoot;

                return $"'{loadedRoot.Name}' was already loaded and is now the active project " +
                       $"({loadedRoot.ProjectFilePath}).";
            }

            var loaded = await context.ProjectExplorerService
                .LoadProjectAsync(projectFile, projectManager, true, true);

            if (loaded is not UniversalFpgaProjectRoot root)
                throw new FpgaAgentException($"'{projectFile}' could not be loaded as an FPGA project.");

            return $"Loaded and activated '{root.Name}' ({root.ProjectFilePath}).";
        });
    }

    private Task<string> SaveProjectAsync()
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();

            await SaveAsync(project);

            return $"Saved '{project.Name}' to {project.ProjectFilePath}.";
        });
    }

    private async Task SaveAsync(UniversalFpgaProjectRoot project)
    {
        if (!await projectManager.SaveProjectAsync(project))
            throw new FpgaAgentException($"'{project.Name}' could not be written to {project.ProjectFilePath}.");
    }

    private string? DescribeToolchain(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var toolchain = fpgaService.Toolchains.FirstOrDefault(x => x.Id == id);

        return toolchain == null ? $"{id} (not installed)" : $"{toolchain.Name} ({toolchain.Id})";
    }

    private string? DescribeLoader(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var loader = fpgaService.Loaders.FirstOrDefault(x => x.Id == id);

        return loader == null ? $"{id} (not installed)" : $"{loader.Name} ({loader.Id})";
    }

    private IFpgaToolchain ResolveToolchain(string idOrName)
    {
        var needle = idOrName.Trim();

        return fpgaService.Toolchains.FirstOrDefault(x =>
                   string.Equals(x.Id, needle, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(x.Name, needle, StringComparison.OrdinalIgnoreCase))
               ?? throw new FpgaAgentException(
                   $"'{idOrName}' is not an available toolchain. Available: " +
                   $"{string.Join(", ", fpgaService.Toolchains.Select(x => x.Id))}.");
    }

    private IFpgaLoader ResolveLoader(string idOrName)
    {
        var needle = idOrName.Trim();

        return fpgaService.Loaders.FirstOrDefault(x =>
                   string.Equals(x.Id, needle, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(x.Name, needle, StringComparison.OrdinalIgnoreCase))
               ?? throw new FpgaAgentException(
                   $"'{idOrName}' is not an available loader. Available: " +
                   $"{string.Join(", ", fpgaService.Loaders.Select(x => x.Id))}.");
    }

    private IFpgaProjectTemplate ResolveTemplate(string name)
    {
        var needle = name.Trim();

        return fpgaService.Templates.FirstOrDefault(x =>
                   string.Equals(x.Name, needle, StringComparison.OrdinalIgnoreCase))
               ?? throw new FpgaAgentException(
                   $"'{name}' is not an available project template. Available: " +
                   $"{string.Join(", ", fpgaService.Templates.Select(x => x.Name))}.");
    }
}
