using System.ComponentModel;
using System.Text;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services.Ai;

/// <summary>
/// Chat functions that select the FPGA hardware and toolchain of a project and run a compile.
/// </summary>
public sealed class FpgaToolchainFunctions(
    FpgaAgentContext context,
    FpgaService fpgaService,
    UniversalFpgaProjectManager projectManager)
{
    /// <summary>
    /// Creates the functions this group contributes to the chat.
    /// </summary>
    public IEnumerable<IOneWareAiFunction> CreateFunctions()
    {
        yield return new OneWareAiFunction
        {
            Name = "fpga_list_toolchains",
            FriendlyName = "List FPGA toolchains",
            DetailExtractor = FpgaFunctionDetail.Constant("available hardware and tools"),
            Description = "Lists everything that can be selected for an FPGA project: toolchains, loaders, " +
                          "simulators, boards, project templates and pre-compile steps, each with the exact id " +
                          "to pass to fpga_set_toolchain, fpga_set_loader and fpga_set_board.",
            Handler = (Func<string>)ListToolchains,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_toolchain",
            FriendlyName = "Select FPGA toolchain",
            DetailExtractor = FpgaFunctionDetail.FirstOf("toolchain", "toolchain"),
            Description = "Selects the toolchain used to compile the active FPGA project. Pass an id from " +
                          "fpga_list_toolchains. This also lets the toolchain write its default project settings.",
            Handler = (Func<string, Task<string>>)SetToolchainAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_loader",
            FriendlyName = "Select FPGA loader",
            DetailExtractor = FpgaFunctionDetail.FirstOf("loader", "loader"),
            Description = "Selects the loader (programmer) used to configure the FPGA or write its FLASH " +
                          "memory. Pass an id from fpga_list_toolchains.",
            Handler = (Func<string, Task<string>>)SetLoaderAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_board",
            FriendlyName = "Select FPGA board",
            DetailExtractor = FpgaFunctionDetail.FirstOf("board", "board"),
            Description = "Selects the FPGA board of the active project. A board must be selected before " +
                          "fpga_compile works. Pass a board name from fpga_list_toolchains.",
            Handler = (Func<string, Task<string>>)SetBoardAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_list_top_entities",
            FriendlyName = "List FPGA top entities",
            DetailExtractor = FpgaFunctionDetail.Constant("top entities"),
            Description = "Scans the HDL sources of the active project and returns every entity or module that " +
                          "could serve as the top level, together with the file it is declared in. Files " +
                          "excluded from compilation and test benches are skipped.",
            Handler = (Func<Task<string>>)ListTopEntitiesAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_top_entity",
            FriendlyName = "Set FPGA top entity",
            DetailExtractor = FpgaFunctionDetail.FirstOf("top entity", "topEntity"),
            Description = "Sets the top level entity or module the toolchain synthesizes. Pass a name from " +
                          "fpga_list_top_entities.",
            Handler = (Func<string, Task<string>>)SetTopEntityAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_compile",
            FriendlyName = "Compile FPGA project",
            DetailExtractor = FpgaFunctionDetail.Constant("compile"),
            Description = "Compiles the active FPGA project with its selected toolchain: saves the open files, " +
                          "runs the enabled pre-compile steps and then synthesis, place & route and bitstream " +
                          "generation. The build stops at the first tool that fails. The tool output appears in " +
                          "the Output panel; this function reports success or failure.",
            Handler = (Func<Task<string>>)CompileAsync,
            ConfirmationCheck = _ => "Compile the active FPGA project? This runs the external toolchain.",
            RunOnUiThread = true
        };
    }

    private string ListToolchains()
    {
        return FpgaFunctionGuard.Run(() =>
        {
            var builder = new StringBuilder();

            FpgaFunctionText.AppendList(builder, "Toolchains",
                fpgaService.Toolchains.Select(x => $"{x.Id} — {x.Name}"),
                "(none — install a toolchain plugin such as OSS CAD Suite)");
            FpgaFunctionText.AppendList(builder, "Loaders",
                fpgaService.Loaders.Select(x => $"{x.Id} — {x.Name}"));
            FpgaFunctionText.AppendList(builder, "Simulators",
                fpgaService.Simulators.Select(x => x.Name));
            FpgaFunctionText.AppendList(builder, "Boards",
                fpgaService.FpgaPackages.Select(x => x.Name),
                "(none — install a hardware package)");
            FpgaFunctionText.AppendList(builder, "ProjectTemplates",
                fpgaService.Templates.Select(x => x.Name));
            FpgaFunctionText.AppendList(builder, "PreCompileSteps",
                fpgaService.PreCompileSteps.Select(x => $"{x.Id} — {x.Name}"));

            return builder.ToString().TrimEnd();
        });
    }

    private Task<string> SetToolchainAsync(
        [Description("Id of the toolchain, as returned by fpga_list_toolchains.")]
        string toolchain)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var needle = FpgaFunctionText.Require(toolchain, nameof(toolchain));

            var selected = fpgaService.Toolchains.FirstOrDefault(x =>
                               string.Equals(x.Id, needle, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(x.Name, needle, StringComparison.OrdinalIgnoreCase))
                           ?? throw new FpgaAgentException(
                               $"'{toolchain}' is not an available toolchain. Available: " +
                               $"{string.Join(", ", fpgaService.Toolchains.Select(x => x.Id))}.");

            project.Toolchain = selected.Id;
            selected.OnProjectCreated(project);

            await SaveAsync(project);

            return $"'{project.Name}' now uses the toolchain {selected.Name} ({selected.Id}).";
        });
    }

    private Task<string> SetLoaderAsync(
        [Description("Id of the loader, as returned by fpga_list_toolchains.")]
        string loader)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var needle = FpgaFunctionText.Require(loader, nameof(loader));

            var selected = fpgaService.Loaders.FirstOrDefault(x =>
                               string.Equals(x.Id, needle, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(x.Name, needle, StringComparison.OrdinalIgnoreCase))
                           ?? throw new FpgaAgentException(
                               $"'{loader}' is not an available loader. Available: " +
                               $"{string.Join(", ", fpgaService.Loaders.Select(x => x.Id))}.");

            project.Loader = selected.Id;

            await SaveAsync(project);

            return $"'{project.Name}' now uses the loader {selected.Name} ({selected.Id}).";
        });
    }

    private Task<string> SetBoardAsync(
        [Description("Name of the board, as returned by fpga_list_toolchains.")]
        string board)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var needle = FpgaFunctionText.Require(board, nameof(board));

            var selected = fpgaService.FpgaPackages.FirstOrDefault(x =>
                               string.Equals(x.Name, needle, StringComparison.OrdinalIgnoreCase))
                           ?? throw new FpgaAgentException(
                               $"'{board}' is not an available board. Available: " +
                               $"{string.Join(", ", fpgaService.FpgaPackages.Select(x => x.Name))}.");

            project.Board = selected.Name;

            await SaveAsync(project);

            return $"'{project.Name}' now targets the board {selected.Name}. Assign the pins in the pin planner " +
                   "before compiling if the design uses IO.";
        });
    }

    private Task<string> ListTopEntitiesAsync()
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var entities = await fpgaService.GetAllTopEntitiesAsync(project);

            if (entities.Count == 0)
                return "No top entity was found. Check that the project contains HDL sources and that the " +
                       "language plugin for them is installed.";

            var builder = new StringBuilder();
            FpgaFunctionText.AppendField(builder, "Current", project.TopEntity);
            FpgaFunctionText.AppendList(builder, "Candidates",
                entities.Select(x => $"{x.TopEntity} — {x.File.RelativePath}"));

            return builder.ToString().TrimEnd();
        });
    }

    private Task<string> SetTopEntityAsync(
        [Description("Name of the entity or module, as returned by fpga_list_top_entities.")]
        string topEntity)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var needle = FpgaFunctionText.Require(topEntity, nameof(topEntity));

            var entities = await fpgaService.GetAllTopEntitiesAsync(project);

            var match = entities.FirstOrDefault(x =>
                string.Equals(x.TopEntity, needle, StringComparison.OrdinalIgnoreCase));

            if (match == null && entities.Count > 0)
                throw new FpgaAgentException(
                    $"'{topEntity}' is not one of the entities found in the project. Available: " +
                    $"{string.Join(", ", entities.Select(x => x.TopEntity))}.");

            project.TopEntity = match?.TopEntity ?? needle;
            project.TopEntityFilePath = match?.File.RelativePath;

            await SaveAsync(project);

            return match == null
                ? $"Set the top entity of '{project.Name}' to '{needle}'. No HDL file declaring it was found, so " +
                  "verify the name."
                : $"Set the top entity of '{project.Name}' to '{match.TopEntity}' ({match.File.RelativePath}).";
        });
    }

    private Task<string> CompileAsync()
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();

            if (string.IsNullOrEmpty(project.Toolchain))
                throw new FpgaAgentException(
                    "No toolchain is selected. Call fpga_list_toolchains and fpga_set_toolchain first.");

            if (string.IsNullOrEmpty(project.Board))
                throw new FpgaAgentException(
                    "No board is selected. Call fpga_list_toolchains and fpga_set_board first.");

            await context.ProjectExplorerService.SaveOpenFilesForProjectAsync(project);

            var success = await fpgaService.RunToolchainAsync(project);

            return success
                ? $"Compiled '{project.Name}' successfully with {project.Toolchain}."
                : $"Compiling '{project.Name}' failed. Read the Output panel for the tool that reported the " +
                  "error; the build stops at the first failing stage, so the first error is the relevant one.";
        });
    }

    private async Task SaveAsync(UniversalFpgaProjectRoot project)
    {
        if (!await projectManager.SaveProjectAsync(project))
            throw new FpgaAgentException($"'{project.Name}' could not be saved.");
    }
}
