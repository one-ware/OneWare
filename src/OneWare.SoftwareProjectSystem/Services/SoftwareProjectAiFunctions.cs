using System.ComponentModel;
using System.Text;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.SoftwareProjectSystem.Models;
using OneWare.SoftwareProjectSystem.Templates;

namespace OneWare.SoftwareProjectSystem.Services;

/// <summary>
/// Chat functions that create, open and describe C/C++ software projects (<c>.swproj</c>).
/// </summary>
public sealed class SoftwareProjectAiFunctions(
    SoftwareProjectFactory factory,
    SoftwareProjectManager manager,
    ToolchainKitService kitService,
    IProjectExplorerService projectExplorerService,
    IMainDockService mainDockService,
    IPaths paths)
{
    public void Register(IAiFunctionProvider provider)
    {
        foreach (var function in CreateFunctions()) provider.RegisterFunction(function);

        provider.RegisterPromptAddition(
            "C/C++ projects in ONE WARE Studio are .swproj files (build, run and debug with gcc/clang, " +
            "optionally CMake). Use cpp_create_project / cpp_open_project for them; for a plain directory " +
            "without project file use folder_open_project.");
    }

    internal IEnumerable<IOneWareAiFunction> CreateFunctions()
    {
        yield return new OneWareAiFunction
        {
            Name = "cpp_list_options",
            FriendlyName = "List C/C++ project options",
            DetailExtractor = _ => "toolchains and templates",
            Description = "Lists the toolchain kits (compilers) detected on this machine, the project templates " +
                          "and the build systems available for C/C++ projects (.swproj). Call this before " +
                          "cpp_create_project to get valid kit and template names.",
            Handler = (Func<string>)ListOptions,
            IsReadOnly = true
        };

        yield return new OneWareAiFunction
        {
            Name = "cpp_create_project",
            FriendlyName = "Create C/C++ project",
            DetailExtractor = args => args.TryGetValue("name", out var name) ? name?.ToString() : null,
            Description = "Creates a new C/C++ project (.swproj) in its own folder, optionally filled from a " +
                          "template, loads it in the project explorer and makes it active. The project builds " +
                          "with the IDE's Build / Run / Debug actions; no Makefile is needed.",
            Handler = (Func<string, string?, string?, string?, bool, Task<string>>)CreateProjectAsync,
            ConfirmationCheck = _ => "Create a new C/C++ project? This creates a folder and a .swproj file on disk.",
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "cpp_open_project",
            FriendlyName = "Open C/C++ project",
            DetailExtractor = args => args.TryGetValue("path", out var path) ? path?.ToString() : null,
            Description = "Loads an existing .swproj file into the project explorer and makes it the active " +
                          "project. Pass the absolute path of the .swproj file.",
            Handler = (Func<string, Task<string>>)OpenProjectAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "cpp_get_project_overview",
            FriendlyName = "Read C/C++ project",
            DetailExtractor = _ => "project overview",
            Description = "Returns the configuration of the active C/C++ project (.swproj): build system, " +
                          "toolchain kit, configuration, output name, language standards, flags, include / " +
                          "exclude patterns and the source files that are compiled. Edit the .swproj JSON " +
                          "file to change settings, then the project reloads automatically on save.",
            Handler = (Func<string>)GetOverview,
            RunOnUiThread = true,
            IsReadOnly = true
        };
    }

    private string ListOptions()
    {
        return Guard(() =>
        {
            kitService.Refresh();
            var builder = new StringBuilder();

            builder.AppendLine("Toolchain kits (pass the id as 'kit'; omit it to use the first host compiler):");
            if (kitService.Kits.Count == 0)
                builder.AppendLine("  (none found - install gcc/g++ or clang and make sure it is on PATH)");
            foreach (var kit in kitService.Kits)
                builder.AppendLine(
                    $"  - {kit.Id}: {kit.Name} ({(kit.IsHost ? "host, can run and debug" : "cross, build only")}; " +
                    $"C: {kit.CCompiler}, C++: {kit.CxxCompiler}{(kit.Gdb != null ? $", gdb: {kit.Gdb}" : "")})");

            builder.AppendLine("Templates (pass the name as 'template'):");
            builder.AppendLine($"  - {SoftwareProjectTemplates.Empty}: only the .swproj file");
            foreach (var template in SoftwareProjectTemplates.All)
                builder.AppendLine($"  - {template.Name} (build system: {template.BuildSystem})");

            builder.AppendLine(
                $"Build systems: '{SoftwareProjectRoot.BuildSystemSimple}' (IDE compiles every source file and " +
                $"links one executable) or '{SoftwareProjectRoot.BuildSystemCMake}' (needs a CMakeLists.txt and " +
                "cmake on PATH).");
            builder.Append($"Default projects directory: {paths.ProjectsDirectory}");
            return builder.ToString();
        });
    }

    private Task<string> CreateProjectAsync(
        [Description("Name of the project. Used for the folder and the .swproj file.")]
        string name,
        [Description("Existing directory the project folder is created in. Defaults to the ONE WARE Studio projects directory.")]
        string? directory = null,
        [Description("Template name as returned by cpp_list_options, e.g. 'C++ Hello World'. Defaults to an empty project.")]
        string? template = null,
        [Description("Toolchain kit id as returned by cpp_list_options. Defaults to the first host compiler.")]
        string? kit = null,
        [Description("Create the project in a new sub-folder named after the project (default true). Set false to turn an existing directory into a project.")]
        bool createFolder = true)
    {
        return GuardAsync(async () =>
        {
            var location = string.IsNullOrWhiteSpace(directory) ? paths.ProjectsDirectory : directory;
            var result = await factory.CreateAsync(name, location, createFolder,
                SoftwareProjectFactory.ResolveTemplate(template), factory.ResolveKit(kit));

            if (result.MainSourceFile != null) await mainDockService.OpenFileAsync(result.MainSourceFile);

            var builder = new StringBuilder();
            builder.AppendLine($"Created and activated the C/C++ project '{result.Root.Name}'.");
            builder.AppendLine($"ProjectFile: {result.Root.ProjectFilePath}");
            builder.AppendLine($"BuildSystem: {result.Root.BuildSystem}");
            builder.AppendLine($"Kit: {result.Root.Kit ?? "auto"}");
            if (result.CreatedFiles.Count > 0)
                builder.AppendLine("Files: " + string.Join(", ",
                    result.CreatedFiles.Select(x => Path.GetRelativePath(result.Root.RootFolderPath, x))));
            builder.Append("The user can now Build, Run and Debug it from the toolbar.");
            return builder.ToString();
        });
    }

    private Task<string> OpenProjectAsync(
        [Description("Absolute path of the .swproj file to load.")]
        string path)
    {
        return GuardAsync(async () =>
        {
            var projectFile = path?.Trim() ?? "";

            if (!projectFile.EndsWith(SoftwareProjectRoot.ProjectFileExtension, StringComparison.OrdinalIgnoreCase))
                throw new SoftwareProjectCreationException(
                    $"'{projectFile}' is not a C/C++ project file ({SoftwareProjectRoot.ProjectFileExtension}). " +
                    "Use folder_open_project for a plain directory.");

            if (!File.Exists(projectFile))
                throw new SoftwareProjectCreationException($"'{projectFile}' does not exist.");

            if (projectExplorerService.Projects.OfType<SoftwareProjectRoot>()
                    .FirstOrDefault(x => x.ProjectFilePath.EqualPaths(projectFile)) is { } loaded)
            {
                projectExplorerService.ActiveProject = loaded;
                return $"'{loaded.Name}' was already loaded and is now the active project.";
            }

            if (await projectExplorerService.LoadProjectAsync(projectFile, manager) is not SoftwareProjectRoot root)
                throw new SoftwareProjectCreationException($"'{projectFile}' could not be loaded as a C/C++ project.");

            projectExplorerService.ActiveProject = root;
            return $"Loaded and activated '{root.Name}' ({root.ProjectFilePath}).";
        });
    }

    private string GetOverview()
    {
        return Guard(() =>
        {
            if (projectExplorerService.ActiveProject is not SoftwareProjectRoot project)
                return "The active project is not a C/C++ project (.swproj). Use cpp_open_project or " +
                       "cpp_create_project.";

            var kit = kitService.GetKitForProject(project);
            var builder = new StringBuilder();
            builder.AppendLine($"Name: {project.Name}");
            builder.AppendLine($"ProjectFile: {project.ProjectFilePath}");
            builder.AppendLine($"RootFolder: {project.RootFolderPath}");
            builder.AppendLine($"BuildSystem: {project.BuildSystem}");
            builder.AppendLine($"Kit: {project.Kit ?? "auto"} -> {(kit == null ? "no compiler found" : $"{kit.Id} ({kit.Name})")}");
            builder.AppendLine($"Configuration: {project.Configuration}");
            builder.AppendLine($"OutputName: {project.OutputName}");
            builder.AppendLine($"BuildDirectory: {project.ConfigurationBuildDirectory}");
            AppendOptional(builder, "CStandard", project.CStandard);
            AppendOptional(builder, "CxxStandard", project.CxxStandard);
            AppendOptional(builder, "RunArguments", project.RunArguments);
            AppendList(builder, "IncludeDirectories", project.IncludeDirectories);
            AppendList(builder, "Defines", project.Defines);
            AppendList(builder, "CompilerFlags", project.CompilerFlags);
            AppendList(builder, "LinkerFlags", project.LinkerFlags);
            AppendList(builder, "Libraries", project.Libraries);
            AppendList(builder, "CMakeArguments", project.CMakeArguments);
            AppendList(builder, "Include", project.Properties.GetStringArray("include") ?? []);
            AppendList(builder, "Exclude", project.Properties.GetStringArray("exclude") ?? []);
            AppendList(builder, "CompileExcluded", project.Properties.GetStringArray("compileExcluded") ?? []);
            AppendList(builder, "SourceFiles", project.GetSourceFiles());
            return builder.ToString().TrimEnd();
        });
    }

    private static void AppendOptional(StringBuilder builder, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value)) builder.AppendLine($"{key}: {value}");
    }

    private static void AppendList(StringBuilder builder, string key, IEnumerable<string> values)
    {
        var list = values.ToArray();
        if (list.Length > 0) builder.AppendLine($"{key}: {string.Join(", ", list)}");
    }

    private static string Guard(Func<string> action)
    {
        try
        {
            return action();
        }
        catch (Exception e)
        {
            return $"Error: {e.Message}";
        }
    }

    private static async Task<string> GuardAsync(Func<Task<string>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception e)
        {
            return $"Error: {e.Message}";
        }
    }
}
