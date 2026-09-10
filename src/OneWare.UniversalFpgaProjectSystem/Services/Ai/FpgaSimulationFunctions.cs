using System.ComponentModel;
using System.Text;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Context;

namespace OneWare.UniversalFpgaProjectSystem.Services.Ai;

/// <summary>
/// Chat functions that configure and run the simulation of a test bench.
/// </summary>
/// <remarks>
/// The per-test-bench configuration lives in a <c>.tbconf</c> file next to the test bench, not in
/// the project file, so these functions read and write that file directly.
/// </remarks>
public sealed class FpgaSimulationFunctions(FpgaAgentContext context, FpgaService fpgaService)
{
    /// <summary>
    /// Creates the functions this group contributes to the chat.
    /// </summary>
    public IEnumerable<IOneWareAiFunction> CreateFunctions()
    {
        yield return new OneWareAiFunction
        {
            Name = "fpga_get_testbench_config",
            FriendlyName = "Read test bench config",
            DetailExtractor = FpgaFunctionDetail.FirstOf("test bench config", "path"),
            Description = "Returns the simulation configuration of a test bench, stored in the .tbconf file " +
                          "next to it: the selected simulator and its options, for example TopModule or " +
                          "VerilatorArguments.",
            Handler = (Func<string, Task<string>>)GetTestBenchConfigAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_testbench_simulator",
            FriendlyName = "Select test bench simulator",
            DetailExtractor = FpgaFunctionDetail.AllOf("simulator", "path", "simulator"),
            Description = "Selects the simulator used for a test bench and writes it to the .tbconf file. Pass " +
                          "a simulator name from fpga_list_toolchains. The file must already be marked as a " +
                          "test bench with fpga_set_testbench.",
            Handler = (Func<string, string, Task<string>>)SetTestBenchSimulatorAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_testbench_option",
            FriendlyName = "Set test bench option",
            DetailExtractor = FpgaFunctionDetail.AllOf("test bench option", "path", "key", "value"),
            Description = "Sets one simulator option of a test bench in its .tbconf file. Common keys are " +
                          "TopModule, VerilatorArguments, VerilatorRuntimeArguments, IcarusVerilogArguments and " +
                          "WaveOutputFormat. Pass an empty value to remove the option.",
            Handler = (Func<string, string, string, Task<string>>)SetTestBenchOptionAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_run_simulation",
            FriendlyName = "Run FPGA simulation",
            DetailExtractor = FpgaFunctionDetail.FirstOf("simulation", "path"),
            Description = "Runs the simulator configured for a test bench. The simulator output appears in the " +
                          "Output panel; this function reports whether the run succeeded. Select the simulator " +
                          "with fpga_set_testbench_simulator first.",
            Handler = (Func<string, Task<string>>)RunSimulationAsync,
            ConfirmationCheck = _ => "Run the test bench simulation? This runs the external simulator.",
            RunOnUiThread = true
        };
    }

    private Task<string> GetTestBenchConfigAsync(
        [Description("Path of the test bench file, relative to the project root or absolute.")]
        string path)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var file = context.RequireFile(project, path);
            var testBenchContext = await TestBenchContextManager.LoadContextAsync(file.FullPath);

            var builder = new StringBuilder();
            FpgaFunctionText.AppendField(builder, "File", file.RelativePath);
            FpgaFunctionText.AppendField(builder, "IsTestBench", project.IsTestBench(file.RelativePath));
            FpgaFunctionText.AppendField(builder, "Simulator", testBenchContext.Simulator);
            FpgaFunctionText.AppendList(builder, "Options",
                testBenchContext.Properties
                    .Where(x => x.Key != "simulator")
                    .Select(x => $"{x.Key} = {x.Value}"),
                "(none — the simulator defaults apply)");

            return builder.ToString().TrimEnd();
        });
    }

    private Task<string> SetTestBenchSimulatorAsync(
        [Description("Path of the test bench file, relative to the project root or absolute.")]
        string path,
        [Description("Name of the simulator, as returned by fpga_list_toolchains.")]
        string simulator)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var file = context.RequireFile(project, path);
            var needle = FpgaFunctionText.Require(simulator, nameof(simulator));

            var selected = ResolveSimulator(needle);

            var testBenchContext = await TestBenchContextManager.LoadContextAsync(file.FullPath);
            testBenchContext.Simulator = selected.Name;

            await SaveContextAsync(testBenchContext, file.RelativePath);

            return project.IsTestBench(file.RelativePath)
                ? $"'{file.RelativePath}' is simulated with {selected.Name}."
                : $"'{file.RelativePath}' is simulated with {selected.Name}. It is not marked as a test bench " +
                  "yet — call fpga_set_testbench to show the simulation toolbar for it.";
        });
    }

    private Task<string> SetTestBenchOptionAsync(
        [Description("Path of the test bench file, relative to the project root or absolute.")]
        string path,
        [Description("Name of the option, for example TopModule or VerilatorArguments.")]
        string key,
        [Description("The new value. Pass an empty string to remove the option.")]
        string value)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var file = context.RequireFile(project, path);
            var optionKey = FpgaFunctionText.Require(key, nameof(key));

            if (string.Equals(optionKey, "simulator", StringComparison.OrdinalIgnoreCase))
                throw new FpgaAgentException("Use fpga_set_testbench_simulator to change the simulator.");

            var testBenchContext = await TestBenchContextManager.LoadContextAsync(file.FullPath);

            if (string.IsNullOrEmpty(value))
                testBenchContext.RemoveBenchProperty(optionKey);
            else
                testBenchContext.SetBenchProperty(optionKey, value);

            await SaveContextAsync(testBenchContext, file.RelativePath);

            return string.IsNullOrEmpty(value)
                ? $"Removed '{optionKey}' from the configuration of '{file.RelativePath}'."
                : $"Set '{optionKey}' to '{value}' for '{file.RelativePath}'.";
        });
    }

    private Task<string> RunSimulationAsync(
        [Description("Path of the test bench file, relative to the project root or absolute.")]
        string path)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var file = context.RequireFile(project, path);

            var testBenchContext = await TestBenchContextManager.LoadContextAsync(file.FullPath);

            if (string.IsNullOrEmpty(testBenchContext.Simulator))
                throw new FpgaAgentException(
                    $"No simulator is selected for '{file.RelativePath}'. Call fpga_set_testbench_simulator " +
                    "first.");

            var simulator = ResolveSimulator(testBenchContext.Simulator);

            await context.ProjectExplorerService.SaveOpenFilesForProjectAsync(project);

            var success = await simulator.SimulateAsync(file.FullPath);

            return success
                ? $"Simulated '{file.RelativePath}' successfully with {simulator.Name}."
                : $"Simulating '{file.RelativePath}' with {simulator.Name} failed. Read the Output panel for " +
                  "the reported error.";
        });
    }

    private IFpgaSimulator ResolveSimulator(string name)
    {
        var needle = name.Trim();

        return fpgaService.Simulators.FirstOrDefault(x =>
                   string.Equals(x.Name, needle, StringComparison.OrdinalIgnoreCase))
               ?? throw new FpgaAgentException(
                   $"'{name}' is not an available simulator. Available: " +
                   $"{string.Join(", ", fpgaService.Simulators.Select(x => x.Name))}.");
    }

    private static async Task SaveContextAsync(TestBenchContext testBenchContext, string relativePath)
    {
        if (!await TestBenchContextManager.SaveContextAsync(testBenchContext))
            throw new FpgaAgentException($"The configuration of '{relativePath}' could not be written.");
    }
}
