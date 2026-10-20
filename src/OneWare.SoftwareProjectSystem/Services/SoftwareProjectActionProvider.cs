using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Debugger;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.ProjectActions;
using OneWare.Essentials.Services;
using OneWare.SoftwareProjectSystem.Build;
using OneWare.SoftwareProjectSystem.Models;

namespace OneWare.SoftwareProjectSystem.Services;

/// <summary>
/// Build, Clean, Run and Debug for <see cref="SoftwareProjectRoot" />s.
/// </summary>
public class SoftwareProjectActionProvider(
    ToolchainKitService kitService,
    IProjectActionService projectActionService,
    IOutputService outputService,
    IErrorService errorService,
    IMainDockService mainDockService,
    DebuggerViewModel debuggerViewModel,
    IServiceProvider serviceProvider,
    ILogger logger) : IProjectActionProvider
{
    public const string ProviderId = "software";
    public const string ErrorSource = "C/C++ Build";

    private readonly SimpleBuildSystem _simpleBuild = new();
    private readonly CMakeBuildSystem _cmakeBuild = new();

    public string Id => ProviderId;

    public bool Supports(IProjectRoot project, ProjectActionKind kind)
    {
        if (project is not SoftwareProjectRoot root) return false;

        return kind switch
        {
            ProjectActionKind.Build or ProjectActionKind.Clean => true,
            ProjectActionKind.Run or ProjectActionKind.Debug => kitService.GetKitForProject(root) is not { IsHost: false },
            _ => false
        };
    }

    public Task<ProjectActionResult> ExecuteAsync(ProjectActionRequest request)
    {
        var root = (SoftwareProjectRoot)request.Project;
        return request.Kind switch
        {
            ProjectActionKind.Build => BuildAsync(root, request.CancellationToken),
            ProjectActionKind.Clean => CleanAsync(root, request.CancellationToken),
            ProjectActionKind.Run => RunAsync(root, request.CancellationToken),
            ProjectActionKind.Debug => DebugAsync(root),
            _ => Task.FromResult(ProjectActionResult.Failed($"{request.Kind} is not supported."))
        };
    }

    private ISoftwareBuildSystem GetBuildSystem(SoftwareProjectRoot root)
    {
        return root.BuildSystem.Equals(SoftwareProjectRoot.BuildSystemCMake, StringComparison.OrdinalIgnoreCase)
            ? _cmakeBuild
            : _simpleBuild;
    }

    private async Task<ProjectActionResult> BuildAsync(SoftwareProjectRoot root, CancellationToken cancellationToken)
    {
        errorService.Clear(ErrorSource);

        var kit = kitService.GetKitForProject(root);
        if (kit == null)
        {
            var message = root.Kit == null
                ? "No C/C++ compiler found. Install GCC or Clang and make sure it is on PATH."
                : $"The toolchain kit '{root.Kit}' is not available.";
            WriteOutput(message, Brushes.Red, root);
            return ProjectActionResult.Failed(message);
        }

        var buildSystem = GetBuildSystem(root);
        var diagnostics = new List<CompilerDiagnostic>();

        WriteOutput($"Building {root.Header} ({root.Configuration}, {kit.Name}, {buildSystem.Id})...", null, root);

        var buildRequest = CreateRequest(root, kit, line =>
        {
            if (GccDiagnosticParser.TryParse(line, root.RootFolderPath, out var diagnostic))
                lock (diagnostics)
                {
                    diagnostics.Add(diagnostic);
                }

            WriteOutput(line, null, root);
        });

        SoftwareBuildResult result;
        try
        {
            result = await Task.Run(() => buildSystem.BuildAsync(buildRequest, cancellationToken),
                cancellationToken);
        }
        finally
        {
            PublishDiagnostics(root, diagnostics);
        }

        if (!result.Success)
        {
            WriteOutput(result.Message ?? "Build failed.", Brushes.Red, root);
            return ProjectActionResult.Failed(result.Message);
        }

        if (result.Message != null) WriteOutput(result.Message, Brushes.Orange, root);
        WriteOutput("Build succeeded.", Brushes.Green, root);

        if (result.Executable == null) return ProjectActionResult.Succeeded();

        var artifact = new BuildArtifact(kit.IsHost ? BuildArtifactKind.Executable : BuildArtifactKind.Elf,
            result.Executable, kit.Name);
        return ProjectActionResult.Succeeded([artifact]);
    }

    private async Task<ProjectActionResult> CleanAsync(SoftwareProjectRoot root, CancellationToken cancellationToken)
    {
        errorService.Clear(ErrorSource);
        var kit = kitService.GetKitForProject(root) ?? new ToolchainKit("none", "None", "cc", "c++");
        var request = CreateRequest(root, kit, _ => { });

        WriteOutput($"Cleaning {request.BuildDirectory}...", null, root);
        try
        {
            await Task.Run(() => GetBuildSystem(root).CleanAsync(request, cancellationToken), cancellationToken);
        }
        catch (IOException e)
        {
            WriteOutput(e.Message, Brushes.Red, root);
            return ProjectActionResult.Failed(e.Message);
        }

        return ProjectActionResult.Succeeded();
    }

    private async Task<ProjectActionResult> RunAsync(SoftwareProjectRoot root, CancellationToken cancellationToken)
    {
        if (await BuildForLaunchAsync(root) is not { } executable)
            return ProjectActionResult.Failed("Nothing to run.");

        if (!serviceProvider.IsRegistered<ITerminalManagerService>())
            return ProjectActionResult.Failed("Running programs needs the terminal, which is not available.");

        var terminal = serviceProvider.Resolve<ITerminalManagerService>();
        mainDockService.Show(terminal, DockShowLocation.Bottom);

        var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"& '{executable.Replace("'", "''")}'"
            : $"'{executable.Replace("'", "'\\''")}'";
        if (!string.IsNullOrWhiteSpace(root.RunArguments)) command += " " + root.RunArguments;

        await terminal.ExecuteInTerminalAsync(command, $"swproj-run-{root.FullPath}", root.Header,
            root.RootFolderPath, true, null, null, cancellationToken);

        return ProjectActionResult.Succeeded();
    }

    private async Task<ProjectActionResult> DebugAsync(SoftwareProjectRoot root)
    {
        if (await BuildForLaunchAsync(root) is not { } executable)
            return ProjectActionResult.Failed("Nothing to debug.");

        var initCommands = new List<string>();
        if (!string.IsNullOrWhiteSpace(root.RunArguments)) initCommands.Add($"-exec-arguments {root.RunArguments}");

        var started = await debuggerViewModel.StartSessionAsync(new DebugLaunchRequest(GdbSessionLauncher.BackendId,
            executable, null, root.RootFolderPath, initCommands));

        return started
            ? ProjectActionResult.Succeeded()
            : ProjectActionResult.Failed("The debug session could not be started.");
    }

    /// <summary>
    /// Builds (nested in the running Run/Debug action) and returns the program to launch.
    /// </summary>
    private async Task<string?> BuildForLaunchAsync(SoftwareProjectRoot root)
    {
        var build = await projectActionService.ExecuteAsync(ProjectActionKind.Build, root);
        if (!build.Success) return null;

        var executable = build.Artifacts?
            .FirstOrDefault(x => x.Kind is BuildArtifactKind.Executable or BuildArtifactKind.Elf)?.Path;
        if (executable == null || !File.Exists(executable))
        {
            WriteOutput("The build produced no program to launch.", Brushes.Red, root);
            return null;
        }

        return executable;
    }

    private SoftwareBuildRequest CreateRequest(SoftwareProjectRoot root, ToolchainKit kit, Action<string> output)
    {
        var isCMake = root.BuildSystem.Equals(SoftwareProjectRoot.BuildSystemCMake,
            StringComparison.OrdinalIgnoreCase);

        return new SoftwareBuildRequest
        {
            RootDirectory = root.RootFolderPath,
            BuildDirectory = root.ConfigurationBuildDirectory,
            CompileCommandsDirectory = root.BuildDirectory,
            Kit = kit,
            Configuration = root.Configuration,
            OutputName = root.OutputName,
            HasExplicitOutputName = root.HasExplicitOutputName,
            Sources = isCMake ? [] : root.GetSourceFiles(),
            IncludeDirectories = root.IncludeDirectories,
            Defines = root.Defines,
            CompilerFlags = root.CompilerFlags,
            LinkerFlags = root.LinkerFlags,
            Libraries = root.Libraries,
            CMakeArguments = root.CMakeArguments,
            CStandard = root.CStandard,
            CxxStandard = root.CxxStandard,
            Output = output,
            Command = line => WriteOutput(line, Brushes.Gray, root)
        };
    }

    private void PublishDiagnostics(SoftwareProjectRoot root, List<CompilerDiagnostic> diagnostics)
    {
        CompilerDiagnostic[] snapshot;
        lock (diagnostics)
        {
            snapshot = diagnostics.ToArray();
        }

        if (snapshot.Length == 0) return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                foreach (var group in snapshot.GroupBy(x => x.FilePath ?? root.ProjectFilePath))
                {
                    var items = group.Distinct().Select(x => new ErrorListItem(x.Message, x.Type, group.Key,
                        ErrorSource, Math.Max(1, x.Line), x.Column, root: root)).ToList();
                    errorService.RefreshErrors(items, ErrorSource, group.Key);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not publish build diagnostics");
            }
        });
    }

    private void WriteOutput(string text, IBrush? color, SoftwareProjectRoot root)
    {
        Dispatcher.UIThread.Post(() => outputService.WriteLine(text, color, root));
    }
}
