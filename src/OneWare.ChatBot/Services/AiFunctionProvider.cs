using System.ComponentModel;
using Avalonia.Threading;
using Microsoft.Extensions.AI;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Services;

namespace OneWare.ChatBot.Services;

public class AiFunctionProvider(
    IProjectExplorerService projectExplorerService,
    IMainDockService dockService,
    IErrorService errorService,
    ITerminalManagerService terminalManagerService) : IAiFunctionProvider
{
    public event EventHandler<string>? FunctionUsed;
    
    public ICollection<AIFunction> GetTools()
    {
        var getActiveProject = AIFunctionFactory.Create(
            () => WrapWithNotification(
                "getActiveProject",
                () => new
                {
                    activeProject = projectExplorerService.ActiveProject?.FullPath
                }),
            "getActiveProject",
            "Returns the working directory of the active project. If no active project is enabled, it will return null."
        );

        var getOpenFiles = AIFunctionFactory.Create(
            () => WrapWithNotification(
                "getOpenFiles",
                () => new
                {
                    openFiles = dockService.OpenFiles.Select(x => x.Key.FullPath).ToArray()
                }),
            "getOpenFiles",
            """
            Returns the full paths of ALL files currently open in the IDE.
            This is the ONLY way to know which files are open.
            Do not assume or invent open files.
            """
        );

        var getOpenFile = AIFunctionFactory.Create(
            () => WrapWithNotification(
                "getFocusedFile",
                () => new
                {
                    currentFile = dockService.CurrentDocument?.FullPath
                }),
            "getFocusedFile",
            """
            Returns the full path of the currently focused editor file.
            This is the ONLY way to know which file is active.
            """
        );

        var getErrorsForFile = AIFunctionFactory.Create(
            ([Description("path of the file to get errors")] string path) => WrapWithNotification(
                "getErrorsForFile",
                () => new
                {
                    errorsForFile = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var errors = errorService.GetErrors();
                        var errorStrings = errors
                            .Where(x => x.File.FullPath.EqualPaths(path))
                            .Select(x => x.ToString()).ToArray();
                        return errorStrings;
                    })
                }),
            "getErrorsForFile",
            """
            Returns the LSP Errors for the specified path (if any)
            """
        );

        var getErrors = AIFunctionFactory.Create(
            () => WrapWithNotification(
                "getAllErrors",
                () => new
                {
                    errors = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var errors = errorService.GetErrors();
                        var errorStrings = errors
                            .Select(x => x.ToString()).ToArray();
                        return errorStrings;
                    })
                }),
            "getAllErrors",
            "Returns all the errors found by LSP"
        );

        var executeInTerminal = AIFunctionFactory.Create(
            (
                [Description("Shell command to execute")]
                string command,
                [Description("Working directory for execution")]
                string workDir
            ) => WrapWithNotification(
                "runTerminalCommand",
                () => new
                {
                    result = Dispatcher.UIThread.InvokeAsync(async () =>
                        await terminalManagerService.ExecuteInTerminalAsync(
                            command,
                            "Copilot",
                            workDir,
                            true,
                            TimeSpan.FromMinutes(1)))
                }),
            "runTerminalCommand",
            """
            Executes a command in the user's visible terminal.
            This is the ONLY way to run commands.
            Do NOT simulate command execution or output.
            """
        );

        return [getActiveProject, getOpenFiles, getOpenFile, getErrorsForFile, getErrors, executeInTerminal];
    }

    private T WrapWithNotification<T>(string functionName, Func<T> handler)
    {
        Dispatcher.UIThread.Post(() => FunctionUsed?.Invoke(this, functionName));
        return handler();
    }
}