using System.ComponentModel;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Microsoft.Extensions.AI;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;

namespace OneWare.Chat.Services;

public class AiFunctionProvider(
    IProjectExplorerService projectExplorerService,
    IMainDockService dockService,
    IErrorService errorService,
    ITerminalManagerService terminalManagerService,
    IWindowService windowService,
    AiFileEditService aiFileEditService) : IAiFunctionProvider
{
    private readonly HashSet<string> _allowedForSession = new(StringComparer.Ordinal);

    public event EventHandler<AiFunctionStartedEvent>? FunctionStarted;
    
    public event EventHandler<AiFunctionPermissionRequestEvent>? FunctionPermissionRequested;

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
                        var resolvedRoot = ResolvePath(rootPath);
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
                    async () =>
                    {
                        var resolvedPath = ResolvePath(path);
                        if (string.IsNullOrWhiteSpace(resolvedPath))
                            return new { result = (string?)null, error = (string?)"No active project and no path provided." };

                        return new
                        {
                            result = await aiFileEditService.ReadFileAsync(resolvedPath, startLine, lineCount),
                            error = (string?)null
                        };
                    }),
            "readFile",
            "Read the specified file (optionally by line range). Relative paths are resolved against the active project."
        );

        var editFile = AIFunctionFactory.Create(
            ([Description("path of the file to edit")] string path,
                [Description("new text to write (full file or replacement lines)")] string content,
                [Description("1-based start line for partial edits (omit for full file)")] int? startLine = null,
                [Description("number of lines to replace from startLine; 0 inserts before startLine")] int? lineCount = null) =>
            WrapWithNotificationTaskUiThread(
                $"Edit File {Path.GetFileName(path)}{((startLine != null && lineCount != null) ? $" Line: {startLine} - {startLine + lineCount - 1}" : "")}",
                "",
                async () =>
                {
                    var resolvedPath = ResolvePath(path);
                    if (string.IsNullOrWhiteSpace(resolvedPath))
                        return new { result = false, error = (string?)"No active project and no path provided." };

                    return new
                    {
                        result = await aiFileEditService.EditFileAsync(resolvedPath, content, startLine, lineCount),
                        error = (string?)null
                    };
                },
                requiresPermission: true,
                permissionScope: "editFile",
                permissionQuestion: "Allow edit this file?",
                permissionDetail: $"File: `{path}`"),
            "editFile",
            "Edit file contents with new text (optionally by line range). Creates missing files automatically. Relative paths are resolved against the active project."
        );

        var listDirectory = AIFunctionFactory.Create(
            ([Description("directory path (optional, defaults to active project root)")] string? path = null,
                [Description("include nested files and folders recursively")] bool recursive = false,
                [Description("include hidden entries")] bool includeHidden = false,
                [Description("maximum number of entries to return")] int maxEntries = 500) =>
                WrapWithNotificationTask(
                    "List Directory",
                    () =>
                    {
                        var resolvedPath = ResolvePath(path);
                        if (string.IsNullOrWhiteSpace(resolvedPath))
                            return Task.FromResult<object>(new { entries = Array.Empty<DirectoryEntry>(), error = "No active project and no path provided." });

                        if (!Directory.Exists(resolvedPath))
                            return Task.FromResult<object>(new { entries = Array.Empty<DirectoryEntry>(), error = $"Directory does not exist: {resolvedPath}" });

                        var entries = EnumerateDirectoryEntries(resolvedPath, recursive, includeHidden, Math.Max(1, maxEntries));
                        return Task.FromResult<object>(new { root = resolvedPath, entries });
                    }),
            "listDirectory",
            "Lists files and folders for a directory. Use this before reading/editing unknown paths."
        );

        var pathExists = AIFunctionFactory.Create(
            ([Description("path to check")] string path) => WrapWithNotificationUiThread(
                $"Check Path {Path.GetFileName(path)}",
                () =>
                {
                    var resolvedPath = ResolvePath(path);
                    if (string.IsNullOrWhiteSpace(resolvedPath))
                        return new { path = (string?)null, exists = false, isFile = false, isDirectory = false, error = (string?)"No active project and no path provided." };

                    var isFile = File.Exists(resolvedPath);
                    var isDirectory = Directory.Exists(resolvedPath);
                    return new
                    {
                        path = (string?)resolvedPath,
                        exists = isFile || isDirectory,
                        isFile,
                        isDirectory,
                        error = (string?)null
                    };
                }),
            "pathExists",
            "Checks whether a path exists and whether it is a file or directory."
        );

        var createDirectory = AIFunctionFactory.Create(
            ([Description("directory path to create")] string path) => WrapWithNotificationUiThread(
                $"Create Directory {Path.GetFileName(path)}",
                () =>
                {
                    var resolvedPath = ResolvePath(path);
                    if (string.IsNullOrWhiteSpace(resolvedPath))
                        return new { created = false, path = (string?)null, error = (string?) "No active project and no path provided." };

                    Directory.CreateDirectory(resolvedPath);
                    return new { created = true, path = (string?) resolvedPath, error = (string?)null };
                }),
            "createDirectory",
            "Creates a directory and any missing parent directories."
        );

        var movePath = AIFunctionFactory.Create(
            ([Description("source file or directory path")] string sourcePath,
                [Description("destination file or directory path")] string destinationPath,
                [Description("overwrite destination if it already exists")] bool overwrite = false) =>
                WrapWithNotificationUiThread(
                    $"Move Path {Path.GetFileName(sourcePath)}",
                    () =>
                    {
                        var resolvedSource = ResolvePath(sourcePath);
                        var resolvedDestination = ResolvePath(destinationPath);
                        if (string.IsNullOrWhiteSpace(resolvedSource) || string.IsNullOrWhiteSpace(resolvedDestination))
                            return new { moved = false, source = resolvedSource, destination = resolvedDestination, kind = (string?)null, error = (string?)"No active project and no path provided." };

                        if (File.Exists(resolvedSource))
                        {
                            EnsureParentDirectory(resolvedDestination);
                            File.Move(resolvedSource, resolvedDestination, overwrite);
                            return new { moved = true, source = (string?)resolvedSource, destination = (string?)resolvedDestination, kind = (string?)"file", error = (string?)null };
                        }

                        if (Directory.Exists(resolvedSource))
                        {
                            if (Directory.Exists(resolvedDestination) && overwrite)
                                Directory.Delete(resolvedDestination, true);
                            Directory.Move(resolvedSource, resolvedDestination);
                            return new { moved = true, source = (string?)resolvedSource, destination = (string?)resolvedDestination, kind = (string?)"directory", error = (string?)null };
                        }

                        return new { moved = false, source = (string?)resolvedSource, destination = (string?)resolvedDestination, kind = (string?)null, error = (string?)$"Source path does not exist: {resolvedSource}" };
                    }),
            "movePath",
            "Moves or renames a file/directory."
        );

        var deletePath = AIFunctionFactory.Create(
            ([Description("file or directory path to delete")] string path,
                [Description("for directories: delete recursively")] bool recursive = true) =>
                WrapWithNotificationUiThread(
                    $"Delete Path {Path.GetFileName(path)}",
                    () =>
                    {
                        var resolvedPath = ResolvePath(path);
                        if (string.IsNullOrWhiteSpace(resolvedPath))
                            return new { deleted = false, path = (string?)null, kind = (string?)null, error = (string?)"No active project and no path provided." };

                        if (File.Exists(resolvedPath))
                        {
                            File.Delete(resolvedPath);
                            return new { deleted = true, path = (string?)resolvedPath, kind = (string?)"file", error = (string?)null };
                        }

                        if (Directory.Exists(resolvedPath))
                        {
                            Directory.Delete(resolvedPath, recursive);
                            return new { deleted = true, path = (string?)resolvedPath, kind = (string?)"directory", error = (string?)null };
                        }

                        return new { deleted = false, path = (string?)resolvedPath, kind = (string?)null, error = (string?)"Path does not exist." };
                    }),
            "deletePath",
            "Deletes a file or directory."
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
                    openFiles = dockService.OpenFiles.Select(x => x.Key).ToArray()
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
                    var resolvedPath = ResolvePath(path);
                    if (string.IsNullOrWhiteSpace(resolvedPath))
                        return new { path = (string?)null, errorsForFile = Array.Empty<string>(), error = (string?)"No active project and no path provided." };

                    var errors = errorService.GetErrors();
                    var errorStrings = errors
                        .Where(x => x.FilePath.EqualPaths(resolvedPath))
                        .Select(x => x.ToString()).ToArray();

                    return new
                    {
                        path = (string?)resolvedPath,
                        errorsForFile = errorStrings,
                        error = (string?)null
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
                [Description("Working directory for execution (WARNING!, only works the first time for every session, after that it needs to be adjusted manually)")]
                string? workDir = null
            ) => WrapWithNotificationTaskUiThread(
                $"Execute In Terminal", command,
                async () =>
                {
                    var resolvedWorkDir = ResolvePath(workDir);

                    var terminalResult = await terminalManagerService.ExecuteInTerminalAsync(
                        command,
                        "Copilot",
                        resolvedWorkDir,
                        true,
                        TimeSpan.FromMinutes(1));

                    return new
                    {
                        result = terminalResult
                    };
                },
                requiresPermission: true,
                permissionScope: "runTerminalCommand",
                permissionQuestion: "Allow this command?",
                permissionDetail: $"```bash\n{command}\n```"),
            "runTerminalCommand",
            """
            Executes a command in the terminal. It will return the result
            """
        );
        
        var openSettings = AIFunctionFactory.Create(
            () => WrapWithNotificationUiThread(
                "Open Settings",
                () =>
                {
                    _ = windowService.ShowDialogAsync(new ApplicationSettingsView
                    {
                        DataContext = ContainerLocator.Container.Resolve<ApplicationSettingsViewModel>()
                    });
                    return true;
                }),
            "openIDESettings",
            "Opens the IDE Setting Dialog"
        );

        return
        [
            getActiveProject, getOpenFile, getOpenFiles, listDirectory, pathExists, createDirectory, movePath, deletePath,
            searchFiles, readFile, editFile, getErrorsForFile, getErrors, executeInTerminal, openSettings
        ];
    }

    private async Task<AiFunctionPermissionDecision> RequestPermissionAsync(
        string functionName,
        string question,
        string? detail)
    {
        var requestId = Guid.NewGuid().ToString();
        var decisionSource =
            new TaskCompletionSource<AiFunctionPermissionDecision>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Dispatcher.UIThread.InvokeAsync(() =>
            FunctionPermissionRequested?.Invoke(this, new AiFunctionPermissionRequestEvent
            {
                Id = requestId,
                FunctionName = functionName,
                Question = question,
                Detail = detail,
                DecisionSource = decisionSource
            }));

        return await decisionSource.Task.ConfigureAwait(false);
    }

    private async Task<T> WrapWithNotificationUiThread<T>(
        string friendlyName,
        Func<T> handler,
        bool requiresPermission = false,
        string? permissionScope = null,
        string? permissionQuestion = null,
        string? permissionDetail = null)
    {
        await EnsurePermissionAsync(friendlyName, requiresPermission, permissionScope, permissionQuestion,
            permissionDetail);

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
        Func<Task<T>> handler,
        bool requiresPermission = false,
        string? permissionScope = null,
        string? permissionQuestion = null,
        string? permissionDetail = null)
    {
        await EnsurePermissionAsync(friendlyName, requiresPermission, permissionScope, permissionQuestion,
            permissionDetail);

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
        Func<Task<T>> handler,
        bool requiresPermission = false,
        string? permissionScope = null,
        string? permissionQuestion = null,
        string? permissionDetail = null)
    {
        await EnsurePermissionAsync(friendlyName, requiresPermission, permissionScope, permissionQuestion,
            permissionDetail ?? detail);

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

    private async Task EnsurePermissionAsync(
        string friendlyName,
        bool requiresPermission,
        string? permissionScope,
        string? permissionQuestion,
        string? permissionDetail)
    {
        if (!requiresPermission) return;

        var scope = string.IsNullOrWhiteSpace(permissionScope) ? friendlyName : permissionScope;
        if (_allowedForSession.Contains(scope))
            return;

        var question = string.IsNullOrWhiteSpace(permissionQuestion)
            ? $"Allow {friendlyName}?"
            : permissionQuestion;
        var detail = string.IsNullOrWhiteSpace(permissionDetail) ? null : permissionDetail;
        var decision = await RequestPermissionAsync(friendlyName, question, detail);

        switch (decision)
        {
            case AiFunctionPermissionDecision.AllowForSession:
                _allowedForSession.Add(scope);
                return;
            case AiFunctionPermissionDecision.AllowOnce:
                return;
            default:
                throw new InvalidOperationException($"{friendlyName} was denied by user.");
        }
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

    private string? ResolvePath(string? path)
    {
        var activeProjectPath = projectExplorerService.ActiveProject?.FullPath;
        if (string.IsNullOrWhiteSpace(path))
            return activeProjectPath;

        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        if (!string.IsNullOrWhiteSpace(activeProjectPath))
            return Path.GetFullPath(Path.Combine(activeProjectPath, path));

        return Path.GetFullPath(path);
    }

    private static IReadOnlyList<DirectoryEntry> EnumerateDirectoryEntries(
        string rootPath,
        bool recursive,
        bool includeHidden,
        int maxEntries)
    {
        var entries = new List<DirectoryEntry>(Math.Min(maxEntries, 256));
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var entryPath in Directory.EnumerateFileSystemEntries(rootPath, "*", option))
        {
            if (entries.Count >= maxEntries)
                break;

            if (!includeHidden && IsHidden(entryPath))
                continue;

            var isDirectory = Directory.Exists(entryPath);
            var sizeBytes = isDirectory ? 0 : new FileInfo(entryPath).Length;
            entries.Add(new DirectoryEntry(entryPath, isDirectory, sizeBytes));
        }

        return entries;
    }

    private static bool IsHidden(string path)
    {
        var name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(name) && name.StartsWith('.'))
            return true;

        try
        {
            return (File.GetAttributes(path) & FileAttributes.Hidden) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    // Records are serialized to JSON and sent to AI - properties accessed via serialization
#pragma warning disable IDE0051, IDE0052, IDE0044, CS9113
    private sealed record DirectoryEntry(string Path, bool IsDirectory, long? SizeBytes);
    private sealed record SearchMatch(string File, int Line, int Column, string LineText, string Match);
#pragma warning restore IDE0051, IDE0052, IDE0044, CS9113
}
