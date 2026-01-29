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
    ITerminalManagerService terminalManagerService,
    FileEditService fileEditService) : IAiFunctionProvider
{
    public event EventHandler<string>? FunctionStarted;

    public event EventHandler<string>? FunctionCompleted;

    public ICollection<AIFunction> GetTools()
    {
        var readFile = AIFunctionFactory.Create(
            ([Description("path of the file to read")] string path,
                [Description("1-based start line for partial reads (omit for full file)")] int? startLine = null,
                [Description("number of lines to read from startLine (omit for full file)")] int? lineCount = null) =>
                WrapWithNotificationUiThread(
                    $"Read File {Path.GetFileName(path)}{((startLine != null && lineCount != null) ? $" Line: {startLine} - {startLine + lineCount - 1}" : "")}",
                    async () => new
                    {
                        result = await fileEditService.ReadFileAsync(path, startLine, lineCount)
                    }),
            "readFile",
            "Read the specified file (optionally by line range). This is the only way to read files in the application."
        );

        var editFile = AIFunctionFactory.Create(
            ([Description("path of the file to edit")] string path,
                [Description("new text to write (full file or replacement lines)")] string content,
                [Description("1-based start line for partial edits (omit for full file)")] int? startLine = null,
                [Description("number of lines to replace from startLine; 0 inserts before startLine")] int? lineCount = null) =>
            WrapWithNotificationUiThread(
                $"Edit File {Path.GetFileName(path)}",
                async () => new
                {
                    result = await fileEditService.EditFileAsync(path, content, startLine, lineCount)
                }),
            "editFile",
            "Replaces file contents with new text (optionally by line range). This is the only way to edit files in the application."
        );

        var getActiveProject = AIFunctionFactory.Create(
            () => WrapWithNotification(
                "Get Active Project",
                () => new
                {
                    activeProject = projectExplorerService.ActiveProject?.FullPath
                }),
            "getActiveProject",
            "Returns the working directory of the active project. If no active project is enabled, it will return null."
        );

        var getOpenFiles = AIFunctionFactory.Create(
            () => WrapWithNotification(
                "Get Open Files",
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
                "Get Focused File",
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
                $"Get Errors for {Path.GetFileName(path)}",
                () =>
                {
                    var errors = errorService.GetErrors();
                    var errorStrings = errors
                        .Where(x => x.File.FullPath.EqualPaths(path))
                        .Select(x => x.ToString()).ToArray();

                    return new
                    {
                        errorsForFile = errorStrings
                    };
                }),
            "getErrorsForFile",
            """
            Returns the LSP Errors for the specified path (if any)
            """
        );

        var getErrors = AIFunctionFactory.Create(
            () => WrapWithNotification(
                "Get Errors",
                () =>
                {
                    var errors = errorService.GetErrors();
                    var errorStrings = errors
                        .Select(x => x.ToString()).ToArray();

                    return new
                    {
                        errors = errorStrings
                    };
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
            ) => WrapWithNotificationUiThread(
                $"Execute In Terminal: {command}",
                async () =>
                {
                    var terminalResult = await terminalManagerService.ExecuteInTerminalAsync(
                        command,
                        "Copilot",
                        workDir,
                        true,
                        TimeSpan.FromMinutes(1));

                    return new
                    {
                        result = terminalResult
                    };
                }),
            "runTerminalCommand",
            """
            Executes a command in the user's visible terminal.
            This is the ONLY way to run commands.
            Do NOT simulate command execution or output.
            """
        );

        return
        [
            getActiveProject, getOpenFile, getOpenFiles, readFile, editFile, getErrorsForFile, getErrors,
            executeInTerminal
        ];
    }

    private async Task<T> WrapWithNotification<T>(string friendlyName, Func<T> handler)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                FunctionStarted?.Invoke(this, friendlyName);
                return handler();
            }
            finally
            {
                FunctionCompleted?.Invoke(this, friendlyName);
            }
        });
    }

    private async Task<T> WrapWithNotificationUiThread<T>(
        string friendlyName,
        Func<Task<T>> handler)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                FunctionStarted?.Invoke(this, friendlyName);
                return await handler().ConfigureAwait(false);
            }
            finally
            {
                FunctionCompleted?.Invoke(this, friendlyName);
            }
        });
    }

    private async Task<T> WrapWithNotification<T>(
        string friendlyName,
        Func<Task<T>> handler)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionStarted?.Invoke(this, friendlyName));

        try
        {
            return await handler().ConfigureAwait(false);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                FunctionCompleted?.Invoke(this, friendlyName));
        }
    }
}
