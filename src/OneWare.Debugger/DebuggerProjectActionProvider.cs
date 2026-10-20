using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.Models;
using OneWare.Essentials.ProjectActions;

namespace OneWare.Debugger;

/// <summary>
/// Generic Debug action for projects whose type has no debug support of its own: starts GDB through a
/// target preparer, or with the last build artifact / the declared executable.
/// </summary>
public class DebuggerProjectActionProvider(
    DebuggerViewModel debuggerViewModel,
    IDebuggerService debuggerService,
    IProjectActionService projectActionService) : IProjectActionProvider
{
    public const string ProviderId = "debugger-fallback";

    public string Id => ProviderId;

    public int Priority => -100;

    public bool Supports(IProjectRoot project, ProjectActionKind kind)
    {
        if (kind != ProjectActionKind.Debug) return false;

        if (debuggerService.TargetPreparers.Any(CanPrepareSafely)) return true;
        if (projectActionService.GetArtifacts(project).Count > 0) return true;

        // Without a preparer or build output, only projects that declare what to debug get the button.
        // Plain folder projects don't; the debugger panel can still start GDB for them directly.
        return project is IProjectRootWithFile withFile
               && withFile.Properties.GetString(DebuggerModule.ExecutableProperty) is { Length: > 0 };
    }

    public async Task<ProjectActionResult> ExecuteAsync(ProjectActionRequest request)
    {
        return await debuggerViewModel.StartSessionAsync()
            ? ProjectActionResult.Succeeded()
            : ProjectActionResult.Failed("The debug session could not be started.");
    }

    private static bool CanPrepareSafely(IDebugTargetPreparer preparer)
    {
        try
        {
            return preparer.CanPrepare();
        }
        catch
        {
            return false;
        }
    }
}
