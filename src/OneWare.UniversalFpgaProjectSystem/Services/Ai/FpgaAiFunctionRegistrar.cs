using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Services.Ai;

/// <summary>
/// Registers all FPGA chat functions with the IDE chat.
/// </summary>
/// <remarks>
/// This is the only place that knows every function group, which keeps the individual groups free
/// of registration concerns and gives the agent a single, authoritative tool list.
/// </remarks>
public static class FpgaAiFunctionRegistrar
{
    /// <summary>
    /// Registers every FPGA chat function.
    /// </summary>
    /// <param name="aiFunctionProvider">The chat function provider of the IDE.</param>
    /// <param name="fpgaService">Provides the registered toolchains, loaders, simulators and boards.</param>
    /// <param name="projectExplorerService">Resolves and saves the active project.</param>
    /// <param name="projectManager">Loads and saves .fpgaproj files.</param>
    /// <param name="projectSettingsService">Provides the settings plugins registered for a project.</param>
    /// <param name="paths">Provides the default projects directory.</param>
    /// <returns>The names of the registered functions, in registration order.</returns>
    public static IReadOnlyList<string> Register(
        IAiFunctionProvider aiFunctionProvider,
        FpgaService fpgaService,
        IProjectExplorerService projectExplorerService,
        UniversalFpgaProjectManager projectManager,
        IProjectSettingsService projectSettingsService,
        IPaths paths)
    {
        ArgumentNullException.ThrowIfNull(aiFunctionProvider);

        var functions = CreateFunctions(fpgaService, projectExplorerService, projectManager,
            projectSettingsService, paths);

        foreach (var function in functions)
            aiFunctionProvider.RegisterFunction(function);

        return functions.Select(x => x.Name).ToList();
    }

    internal static IReadOnlyList<IOneWareAiFunction> CreateFunctions(
        FpgaService fpgaService,
        IProjectExplorerService projectExplorerService,
        UniversalFpgaProjectManager projectManager,
        IProjectSettingsService projectSettingsService,
        IPaths paths)
    {
        ArgumentNullException.ThrowIfNull(fpgaService);
        ArgumentNullException.ThrowIfNull(projectExplorerService);
        ArgumentNullException.ThrowIfNull(projectManager);
        ArgumentNullException.ThrowIfNull(projectSettingsService);
        ArgumentNullException.ThrowIfNull(paths);

        var context = new FpgaAgentContext(projectExplorerService);

        var functions = new List<IOneWareAiFunction>();
        functions.AddRange(new FpgaProjectFunctions(context, fpgaService, projectManager, paths).CreateFunctions());
        functions.AddRange(new FpgaToolchainFunctions(context, fpgaService, projectManager).CreateFunctions());
        functions.AddRange(new FpgaSettingsFunctions(context, fpgaService, projectManager, projectSettingsService)
            .CreateFunctions());
        functions.AddRange(new FpgaFileFunctions(context, projectManager).CreateFunctions());
        functions.AddRange(new FpgaSimulationFunctions(context, fpgaService).CreateFunctions());
        return functions;
    }
}
