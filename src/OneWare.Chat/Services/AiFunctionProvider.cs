using System.ComponentModel;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Microsoft.Extensions.AI;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Chat.Services;

public class AiFunctionProvider(
    IProjectExplorerService projectExplorerService,
    IMainDockService dockService,
    IErrorService errorService,
    ITerminalManagerService terminalManagerService,
    AiFileEditService aiFileEditService) : IAiFunctionProvider
{
    public event EventHandler<AiFunctionStartedEvent>? FunctionStarted;

    public event EventHandler<AiFunctionCompletedEvent>? FunctionCompleted;

    public ICollection<AIFunction> GetTools()
    {
        var searchFiles = AIFunctionFactory.Create(
            ([Description("regex pattern to search for")] string pattern,
                [Description("root directory to search (optional, defaults to active project)")] string? rootPath = null,
                [Description("file name glob(s), e.g. \"*.cs\" or \"*.cs;*.md\" (optional)")] string? fileGlob = null,
                [Description("case-insensitive search")] bool ignoreCase = true,
                [Description("max total matches to return")] int maxResults = 200,
                [Description("max matches per file")] int maxResultsPerFile = 20) =>
                WrapWithNotificationTask<object>(
                    "Search Files",
                    async () =>
                    {
                        var resolvedRoot = rootPath ?? projectExplorerService.ActiveProject?.FullPath;
                        if (string.IsNullOrWhiteSpace(resolvedRoot))
                            return new { results = Array.Empty<SearchMatch>(), error = "No active project and no root path provided." };

                        if (!Directory.Exists(resolvedRoot))
                            return new { results = Array.Empty<SearchMatch>(), error = $"Root path does not exist: {resolvedRoot}" };

                        var results = await SearchInFilesAsync(
                            resolvedRoot,
                            pattern,
                            fileGlob,
                            ignoreCase,
                            maxResults,
                            maxResultsPerFile);

                        return new { results };
                    }),
            "searchFiles",
            """
            Searches files using a regular expression.
            Returns matching lines with file path, line number, column, and match text.
            """
        );

        var readFile = AIFunctionFactory.Create(
            ([Description("path of the file to read")] string path,
                [Description("1-based start line for partial reads (omit for full file)")] int? startLine = null,
                [Description("number of lines to read from startLine (omit for full file)")] int? lineCount = null) =>
                WrapWithNotificationTaskUiThread(
                    $"Read File {Path.GetFileName(path)}{((startLine != null && lineCount != null) ? $" Line: {startLine} - {startLine + lineCount - 1}" : "")}", "",
                    async () => new
                    {
                        result = await aiFileEditService.ReadFileAsync(path, startLine, lineCount)
                    }),
            "readFile",
            "Read the specified file (optionally by line range). This is the only way to read files in the application."
        );

        var editFile = AIFunctionFactory.Create(
            ([Description("path of the file to edit")] string path,
                [Description("new text to write (full file or replacement lines)")] string content,
                [Description("1-based start line for partial edits (omit for full file)")] int? startLine = null,
                [Description("number of lines to replace from startLine; 0 inserts before startLine")] int? lineCount = null) =>
            WrapWithNotificationTaskUiThread(
                $"Edit File {Path.GetFileName(path)}{((startLine != null && lineCount != null) ? $" Line: {startLine} - {startLine + lineCount - 1}" : "")}",
                "",
                async () => new
                {
                    result = await aiFileEditService.EditFileAsync(path, content, startLine, lineCount)
                }),
            "editFile",
            "Replaces file contents with new text (optionally by line range). This is the only way to edit files in the application."
        );

        var getActiveProject = AIFunctionFactory.Create(
            () => WrapWithNotificationUiThread(
                "Get Active Project",
                () => new
                {
                    activeProject = projectExplorerService.ActiveProject?.FullPath
                }),
            "getActiveProject",
            "Returns the working directory of the active project. If no active project is enabled, it will return null."
        );

        var getOpenFiles = AIFunctionFactory.Create(
            () => WrapWithNotificationUiThread(
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
            () => WrapWithNotificationUiThread(
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
            ([Description("path of the file to get errors")] string path) => WrapWithNotificationUiThread(
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
            () => WrapWithNotificationUiThread(
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
            ) => WrapWithNotificationTaskUiThread(
                $"Execute In Terminal", command,
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
            getActiveProject, getOpenFile, getOpenFiles, searchFiles, readFile, editFile, getErrorsForFile, getErrors,
            executeInTerminal
        ];
    }

    private async Task<T> WrapWithNotificationUiThread<T>(
        string friendlyName,
        Func<T> handler)
    {
        var id = Guid.NewGuid().ToString();

        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Exception? exception = null;

            try
            {
                FunctionStarted?.Invoke(this, new AiFunctionStartedEvent
                {
                    Id = id,
                    FunctionName = friendlyName
                });

                return handler();
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                FunctionCompleted?.Invoke(this, new AiFunctionCompletedEvent
                {
                    Id = id,
                    Result = exception == null,
                    ToolOutput = exception?.ToString() ?? $"Tool {id} succeeded."
                });
            }
        });
    }

    private async Task<T> WrapWithNotificationTask<T>(
        string friendlyName,
        Func<Task<T>> handler)
    {
        var id = Guid.NewGuid().ToString();

        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionStarted?.Invoke(this, new AiFunctionStartedEvent
            {
                Id = id,
                FunctionName = friendlyName
            }));

        Exception? exception = null;

        try
        {
            return await handler().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                FunctionCompleted?.Invoke(this, new AiFunctionCompletedEvent
                {
                    Id = id,
                    Result = exception == null,
                    ToolOutput = exception?.ToString() ?? $"Tool {id} succeeded."
                }));
        }
    }
    
    private async Task<T> WrapWithNotificationTaskUiThread<T>(
        string friendlyName, string detail,
        Func<Task<T>> handler)
    {
        var id = Guid.NewGuid().ToString();

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            Exception? exception = null;

            try
            {
                FunctionStarted?.Invoke(this, new AiFunctionStartedEvent
                {
                    Id = id,
                    FunctionName = friendlyName,
                    Detail = detail
                });

                // Handler executes; continuation may leave UI thread
                return await handler();
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                FunctionCompleted?.Invoke(this, new AiFunctionCompletedEvent
                {
                    Id = id,
                    Result = exception == null,
                    ToolOutput = exception?.ToString() ?? $"Tool {id} succeeded."
                });
            }
        });
    }
    
    private static async Task<IReadOnlyList<SearchMatch>> SearchInFilesAsync(
        string rootPath,
        string pattern,
        string? fileGlob,
        bool ignoreCase,
        int maxResults,
        int maxResultsPerFile)
    {
        var results = new List<SearchMatch>();
        var options = RegexOptions.Compiled | RegexOptions.Multiline;
        if (ignoreCase)
            options |= RegexOptions.IgnoreCase;

        var regex = new Regex(pattern, options, TimeSpan.FromSeconds(1));
        var globs = ParseGlobs(fileGlob);

        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            if (globs.Length > 0 && !IsGlobMatch(Path.GetFileName(filePath), globs))
                continue;

            var fileMatchCount = 0;
            var lineNumber = 0;

            try
            {
                using var reader = new StreamReader(filePath);
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null) break;

                    lineNumber++;
                    foreach (Match match in regex.Matches(line))
                    {
                        results.Add(new SearchMatch(
                            filePath,
                            lineNumber,
                            match.Index + 1,
                            line,
                            match.Value));

                        fileMatchCount++;
                        if (fileMatchCount >= maxResultsPerFile)
                            break;
                        if (results.Count >= maxResults)
                            return results;
                    }

                    if (fileMatchCount >= maxResultsPerFile)
                        break;
                }
            }
            catch
            {
                // Ignore unreadable files.
            }

            if (results.Count >= maxResults)
                break;
        }

        return results;
    }

    private static string[] ParseGlobs(string? fileGlob)
    {
        if (string.IsNullOrWhiteSpace(fileGlob))
            return Array.Empty<string>();

        return fileGlob.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsGlobMatch(string fileName, string[] globs)
    {
        foreach (var glob in globs)
        {
            var pattern = "^" + Regex.Escape(glob).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    private sealed record SearchMatch(string File, int Line, int Column, string LineText, string Match);
}
