using System.ComponentModel;
using System.Text;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services.Ai;

/// <summary>
/// Chat functions for the per-file flags of an FPGA project: which files are excluded from
/// compilation and which ones are test benches.
/// </summary>
public sealed class FpgaFileFunctions(
    FpgaAgentContext context,
    UniversalFpgaProjectManager projectManager)
{
    /// <summary>
    /// Creates the functions this group contributes to the chat.
    /// </summary>
    public IEnumerable<IOneWareAiFunction> CreateFunctions()
    {
        yield return new OneWareAiFunction
        {
            Name = "fpga_list_files",
            FriendlyName = "List FPGA project files",
            DetailExtractor = FpgaFunctionDetail.FirstOf("project files", "searchPattern"),
            Description = "Lists the files of the active FPGA project with their flags: [excluded] when the " +
                          "file is excluded from compilation, [testbench] when it is registered as a test " +
                          "bench and [top] when it declares the top entity. Use the relative paths it returns " +
                          "for the other fpga_* file functions.",
            Handler = (Func<string?, string>)ListFiles,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_compile_excluded",
            FriendlyName = "Exclude file from compilation",
            DetailExtractor = FpgaFunctionDetail.AllOf("compile exclusion", "path", "excluded"),
            Description = "Excludes a project file from compilation, or includes it again. Excluded files are " +
                          "not passed to synthesis; use this for alternative implementations or for sources " +
                          "that only the simulator needs.",
            Handler = (Func<string, string, Task<string>>)SetCompileExcludedAsync,
            RunOnUiThread = true
        };

        yield return new OneWareAiFunction
        {
            Name = "fpga_set_testbench",
            FriendlyName = "Mark file as test bench",
            DetailExtractor = FpgaFunctionDetail.AllOf("test bench", "path", "isTestBench"),
            Description = "Marks a project file as a test bench, or removes that mark. Test benches are " +
                          "skipped when searching for the top entity and can be run with fpga_run_simulation.",
            Handler = (Func<string, string, Task<string>>)SetTestBenchAsync,
            RunOnUiThread = true
        };
    }

    private string ListFiles(
        [Description("Glob pattern to filter the files, for example '*.v'. Defaults to every file.")]
        string? searchPattern = null)
    {
        return FpgaFunctionGuard.Run(() =>
        {
            var project = context.RequireProject();
            var pattern = string.IsNullOrWhiteSpace(searchPattern) ? "*" : searchPattern.Trim();

            var files = project.GetFiles(pattern)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
                return $"No file of '{project.Name}' matches '{pattern}'.";

            var builder = new StringBuilder();
            builder.AppendLine($"{files.Count} file(s) in '{project.Name}':");

            foreach (var file in files)
            {
                var flags = new List<string>();

                if (project.IsCompileExcluded(file)) flags.Add("excluded");
                if (project.IsTestBench(file)) flags.Add("testbench");
                if (project.TopEntityFilePath == file) flags.Add("top");

                builder.AppendLine($"  - {file}{(flags.Count == 0 ? "" : $" [{string.Join(", ", flags)}]")}");
            }

            return builder.ToString().TrimEnd();
        });
    }

    private Task<string> SetCompileExcludedAsync(
        [Description("Path of the file, relative to the project root or absolute.")]
        string path,
        [Description("true to exclude the file from compilation, false to include it again.")]
        string excluded)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var file = context.RequireFile(project, path);
            var exclude = FpgaFunctionText.ParseBool(excluded, nameof(excluded));

            if (exclude)
                project.AddCompileExcluded(file.RelativePath);
            else
                project.RemoveCompileExcluded(file.RelativePath);

            await SaveAsync(project);

            return exclude
                ? $"'{file.RelativePath}' is now excluded from compilation."
                : $"'{file.RelativePath}' is compiled again.";
        });
    }

    private Task<string> SetTestBenchAsync(
        [Description("Path of the file, relative to the project root or absolute.")]
        string path,
        [Description("true to mark the file as a test bench, false to remove the mark.")]
        string isTestBench)
    {
        return FpgaFunctionGuard.RunAsync(async () =>
        {
            var project = context.RequireProject();
            var file = context.RequireFile(project, path);
            var mark = FpgaFunctionText.ParseBool(isTestBench, nameof(isTestBench));

            if (mark)
                project.AddTestBench(file.RelativePath);
            else
                project.RemoveTestBench(file.RelativePath);

            await SaveAsync(project);

            return mark
                ? $"'{file.RelativePath}' is now a test bench. Select its simulator with " +
                  "fpga_set_testbench_simulator."
                : $"'{file.RelativePath}' is no longer a test bench.";
        });
    }

    private async Task SaveAsync(UniversalFpgaProjectRoot project)
    {
        if (!await projectManager.SaveProjectAsync(project))
            throw new FpgaAgentException($"'{project.Name}' could not be saved.");
    }
}
