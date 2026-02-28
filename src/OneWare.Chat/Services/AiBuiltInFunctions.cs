using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;

namespace OneWare.Chat.Services;

internal static class AiBuiltInFunctions
{
    private const int MaxTerminalOutputLines = 220;
    private const int MaxTerminalOutputChars = 12000;

    public static void Register(
        IAiFunctionProvider functionProvider,
        IProjectExplorerService projectExplorerService,
        IMainDockService dockService,
        IErrorService errorService,
        ITerminalManagerService terminalManagerService,
        IWindowService windowService,
        AiFileEditService aiFileEditService)
    {
        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "searchFiles",
            FriendlyName = "Search Files",
            Description = """
                          Searches files using a regular expression.
                          Returns matching lines with file path, line number, column, and match text.
                          """,
            Handler =
                ([Description("regex pattern to search for")] string pattern,
                    [Description("absolute root directory to search (optional, defaults to active project)")] string?
                        rootPath = null,
                    [Description("file name glob(s), e.g. \"*.cs\" or \"*.cs;*.md\" (optional)")] string?
                        fileGlob = null,
                    [Description("case-insensitive search")] bool ignoreCase = true,
                    [Description("max total matches to return")] int maxResults = 200,
                    [Description("max matches per file")] int maxResultsPerFile = 20) =>
                    SearchFilesAsync(projectExplorerService, pattern, rootPath, fileGlob, ignoreCase, maxResults,
                        maxResultsPerFile)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "readFile",
            FriendlyName = "Read File",
            RunOnUiThread = true,
            Description = "Read the specified file (optionally by line range). Always pass an absolute path.",
            Handler = ([Description("absolute path of the file to read")] string path,
                    [Description("1-based start line for partial reads (omit for full file)")] int? startLine = null,
                    [Description("number of lines to read from startLine (omit for full file)")] int? lineCount =
                        null) =>
                ReadFileAsync(projectExplorerService, aiFileEditService, path, startLine, lineCount)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "editFile",
            FriendlyName = "Edit File",
            RunOnUiThread = true,
            RequirePermission = true,
            PermissionScope = "edit",
            PermissionQuestion = "Allow editing this file?",
            PermissionDetailFactory = args => $"File: `{TryGetStringArgument(args, "path") ?? "<unknown>"}`",
            Description =
                "Edit file contents with new text (optionally by line range). Creates missing files automatically. Always pass an absolute path.",
            Handler = ([Description("absolute path of the file to edit")] string path,
                    [Description("new text to write (full file or replacement lines)")] string content,
                    [Description("1-based start line for partial edits (omit for full file)")] int? startLine = null,
                    [Description("number of lines to replace from startLine; 0 inserts before startLine")] int?
                        lineCount = null) =>
                EditFileAsync(projectExplorerService, aiFileEditService, path, content, startLine, lineCount)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "listDirectory",
            FriendlyName = "List Directory",
            Description = "Lists files and folders for a directory. Use this before reading/editing unknown paths.",
            Handler = ([Description("absolute directory path (optional, defaults to active project root)")] string?
                    path = null,
                    [Description("include nested files and folders recursively")] bool recursive = false,
                    [Description("include hidden entries")] bool includeHidden = false,
                    [Description("maximum number of entries to return")] int maxEntries = 500) =>
                ListDirectory(projectExplorerService, path, recursive, includeHidden, maxEntries)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "pathExists",
            FriendlyName = "Check Path",
            RunOnUiThread = true,
            Description = "Checks whether a path exists and whether it is a file or directory.",
            Handler = ([Description("absolute path to check")] string path) => PathExists(projectExplorerService, path)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "createDirectory",
            FriendlyName = "Create Directory",
            RunOnUiThread = true,
            Description = "Creates a directory and any missing parent directories.",
            Handler = ([Description("absolute directory path to create")] string path) =>
                CreateDirectory(projectExplorerService, path)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "movePath",
            FriendlyName = "Move Path",
            RunOnUiThread = true,
            RequirePermission = true,
            PermissionScope = "edit",
            PermissionQuestion = "Move or rename a file/directory.",
            PermissionDetailFactory = args =>
                $"{TryGetStringArgument(args, "sourcePath") ?? "<source>"} -> {TryGetStringArgument(args, "destinationPath") ?? "<destination>"}",
            Description = "Moves or renames a file/directory.",
            Handler = ([Description("absolute source file or directory path")] string sourcePath,
                    [Description("absolute destination file or directory path")] string destinationPath,
                    [Description("overwrite destination if it already exists")] bool overwrite = false) =>
                MovePath(projectExplorerService, sourcePath, destinationPath, overwrite)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "deletePath",
            FriendlyName = "Delete Path",
            RunOnUiThread = true,
            RequirePermission = true,
            PermissionScope = "edit",
            PermissionQuestion = "Delete a file or directory.",
            PermissionDetailFactory = args => TryGetStringArgument(args, "path") ?? "<unknown>",
            Description = "Deletes a file or directory.",
            Handler = ([Description("absolute file or directory path to delete")] string path,
                    [Description("for directories: delete recursively")] bool recursive = true) =>
                DeletePath(projectExplorerService, path, recursive)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "getActiveProject",
            FriendlyName = "Get Active Project",
            RunOnUiThread = true,
            Description =
                "Returns the absolute working directory of the active project. If no active project is enabled, it returns null.",
            Handler = () => new
            {
                activeProject = ResolvePath(projectExplorerService, null)
            }
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "getOpenFiles",
            FriendlyName = "Get Open Files",
            RunOnUiThread = true,
            Description = """
                          Returns the absolute full paths of ALL files currently open in the IDE.
                          This is the ONLY way to know which files are open.
                          Do not assume or invent open files.
                          """,
            Handler = () => new
            {
                openFiles = dockService.OpenFiles
                    .Select(x => ResolvePath(projectExplorerService, x.Key))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray()
            }
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "getFocusedFile",
            FriendlyName = "Get Focused File",
            RunOnUiThread = true,
            Description = """
                          Returns the absolute full path of the currently focused editor file.
                          This is the ONLY way to know which file is active.
                          """,
            Handler = () => new
            {
                currentFile = ResolvePath(projectExplorerService, dockService.CurrentDocument?.FullPath)
            }
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "getErrorsForFile",
            FriendlyName = "Get Errors for File",
            RunOnUiThread = true,
            Description = "Returns the LSP Errors for the specified path (if any)",
            Handler = ([Description("absolute path of the file to get errors")] string path) =>
                GetErrorsForFile(projectExplorerService, errorService, path)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "getAllErrors",
            FriendlyName = "Get Errors",
            RunOnUiThread = true,
            Description = "Returns all the errors found by LSP",
            Handler = () => new
            {
                errors = errorService.GetErrors().Select(x => x.ToString()).ToArray()
            }
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "runTerminalCommand",
            FriendlyName = "Execute In Terminal",
            RunOnUiThread = true,
            RequirePermission = true,
            PermissionScope = "runTerminalCommand",
            PermissionQuestion = "Allow this command?",
            PermissionDetailFactory = args =>
                $"```bash\n{TryGetStringArgument(args, "command") ?? "<unknown command>"}\n```",
            Description = """
                          Executes a command in the terminal and returns the output.
                          This is the only supported way to execute shell commands.
                          Output is automatically truncated to avoid oversized responses.
                          """,
            Handler = ([Description("Shell command to execute")] string command,
                    [Description("Absolute working directory for execution (optional, defaults to active project).")]
                    string? workDir = null) =>
                RunTerminalCommandAsync(projectExplorerService, terminalManagerService, command, workDir)
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "openIDESettings",
            FriendlyName = "Open Settings",
            RunOnUiThread = true,
            Description = "Opens the IDE Setting Dialog",
            Handler = () =>
            {
                _ = windowService.ShowDialogAsync(new ApplicationSettingsView
                {
                    DataContext = ContainerLocator.Container.Resolve<ApplicationSettingsViewModel>()
                });
                return true;
            }
        });
    }

    private static async Task<object> SearchFilesAsync(
        IProjectExplorerService projectExplorerService,
        string pattern,
        string? rootPath,
        string? fileGlob,
        bool ignoreCase,
        int maxResults,
        int maxResultsPerFile)
    {
        var resolvedRoot = ResolvePath(projectExplorerService, rootPath);
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
    }

    private static async Task<object> ReadFileAsync(
        IProjectExplorerService projectExplorerService,
        AiFileEditService aiFileEditService,
        string path,
        int? startLine,
        int? lineCount)
    {
        var resolvedPath = ResolvePath(projectExplorerService, path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return new { result = (string?)null, error = (string?)"No active project and no path provided." };

        return new
        {
            result = await aiFileEditService.ReadFileAsync(resolvedPath, startLine, lineCount),
            error = (string?)null
        };
    }

    private static async Task<object> EditFileAsync(
        IProjectExplorerService projectExplorerService,
        AiFileEditService aiFileEditService,
        string path,
        string content,
        int? startLine,
        int? lineCount)
    {
        var resolvedPath = ResolvePath(projectExplorerService, path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return new { result = false, error = (string?)"No active project and no path provided." };

        return new
        {
            result = await aiFileEditService.EditFileAsync(resolvedPath, content, startLine, lineCount),
            error = (string?)null
        };
    }

    private static object ListDirectory(
        IProjectExplorerService projectExplorerService,
        string? path,
        bool recursive,
        bool includeHidden,
        int maxEntries)
    {
        var resolvedPath = ResolvePath(projectExplorerService, path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return new { entries = Array.Empty<DirectoryEntry>(), error = "No active project and no path provided." };

        if (!Directory.Exists(resolvedPath))
            return new { entries = Array.Empty<DirectoryEntry>(), error = $"Directory does not exist: {resolvedPath}" };

        var entries = EnumerateDirectoryEntries(resolvedPath, recursive, includeHidden, Math.Max(1, maxEntries));
        return new { root = resolvedPath, entries };
    }

    private static object PathExists(IProjectExplorerService projectExplorerService, string path)
    {
        var resolvedPath = ResolvePath(projectExplorerService, path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return new
                { path = (string?)null, exists = false, isFile = false, isDirectory = false, error = (string?)"No active project and no path provided." };

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
    }

    private static object CreateDirectory(IProjectExplorerService projectExplorerService, string path)
    {
        var resolvedPath = ResolvePath(projectExplorerService, path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return new { created = false, path = (string?)null, error = (string?)"No active project and no path provided." };

        Directory.CreateDirectory(resolvedPath);
        return new { created = true, path = (string?)resolvedPath, error = (string?)null };
    }

    private static object MovePath(
        IProjectExplorerService projectExplorerService,
        string sourcePath,
        string destinationPath,
        bool overwrite)
    {
        var resolvedSource = ResolvePath(projectExplorerService, sourcePath);
        var resolvedDestination = ResolvePath(projectExplorerService, destinationPath);
        if (string.IsNullOrWhiteSpace(resolvedSource) || string.IsNullOrWhiteSpace(resolvedDestination))
            return new
                { moved = false, source = resolvedSource, destination = resolvedDestination, kind = (string?)null, error = (string?)"No active project and no path provided." };

        if (File.Exists(resolvedSource))
        {
            EnsureParentDirectory(resolvedDestination);
            File.Move(resolvedSource, resolvedDestination, overwrite);
            return new
                { moved = true, source = (string?)resolvedSource, destination = (string?)resolvedDestination, kind = (string?)"file", error = (string?)null };
        }

        if (Directory.Exists(resolvedSource))
        {
            if (Directory.Exists(resolvedDestination) && overwrite)
                Directory.Delete(resolvedDestination, true);
            Directory.Move(resolvedSource, resolvedDestination);
            return new
                { moved = true, source = (string?)resolvedSource, destination = (string?)resolvedDestination, kind = (string?)"directory", error = (string?)null };
        }

        return new
            { moved = false, source = (string?)resolvedSource, destination = (string?)resolvedDestination, kind = (string?)null, error = (string?)$"Source path does not exist: {resolvedSource}" };
    }

    private static object DeletePath(IProjectExplorerService projectExplorerService, string path, bool recursive)
    {
        var resolvedPath = ResolvePath(projectExplorerService, path);
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
    }

    private static object GetErrorsForFile(
        IProjectExplorerService projectExplorerService,
        IErrorService errorService,
        string path)
    {
        var resolvedPath = ResolvePath(projectExplorerService, path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return new
                { path = (string?)null, errorsForFile = Array.Empty<string>(), error = (string?)"No active project and no path provided." };

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
    }

    private static async Task<object> RunTerminalCommandAsync(
        IProjectExplorerService projectExplorerService,
        ITerminalManagerService terminalManagerService,
        string command,
        string? workDir)
    {
        var resolvedWorkDir = ResolvePath(projectExplorerService, workDir);

        var terminalResult = await terminalManagerService.ExecuteInTerminalAsync(
            command,
            "AI Chat",
            resolvedWorkDir,
            true,
            TimeSpan.FromMinutes(1));

        var truncatedOutput = TruncateTerminalOutput(terminalResult.Output, out var outputTruncated);
        var result = outputTruncated
            ? terminalResult with { Output = truncatedOutput }
            : terminalResult;

        return new
        {
            result,
            outputTruncated,
            originalOutputLength = terminalResult.Output.Length
        };
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
            return [];

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

    private static string TruncateTerminalOutput(string output, out bool wasTruncated)
    {
        var lineLimited = TruncateByLines(output, MaxTerminalOutputLines, out var lineTruncated);
        var charLimited = TruncateByChars(lineLimited, MaxTerminalOutputChars, out var charTruncated);
        wasTruncated = lineTruncated || charTruncated;
        return charLimited;
    }

    private static string TruncateByLines(string output, int maxLines, out bool wasTruncated)
    {
        if (string.IsNullOrEmpty(output))
        {
            wasTruncated = false;
            return output;
        }

        var normalized = output.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        if (lines.Length <= maxLines)
        {
            wasTruncated = false;
            return output;
        }

        wasTruncated = true;
        var headCount = maxLines / 2;
        var tailCount = maxLines - headCount;
        var omittedCount = lines.Length - maxLines;

        var head = lines.Take(headCount);
        var tail = lines.Skip(lines.Length - tailCount);
        var marker = $"... [truncated {omittedCount} lines] ...";

        return string.Join('\n', head.Concat([marker]).Concat(tail));
    }

    private static string TruncateByChars(string output, int maxChars, out bool wasTruncated)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= maxChars)
        {
            wasTruncated = false;
            return output;
        }

        wasTruncated = true;
        var marker = "\n... [truncated output] ...\n";
        var budget = Math.Max(0, maxChars - marker.Length);
        var headCount = budget / 2;
        var tailCount = budget - headCount;

        return output[..headCount] + marker + output[^tailCount..];
    }

    private static string? ResolvePath(IProjectExplorerService projectExplorerService, string? path)
    {
        var activeProjectPath = projectExplorerService.ActiveProject?.FullPath;
        var normalizedActiveProjectPath = string.IsNullOrWhiteSpace(activeProjectPath)
            ? null
            : Path.GetFullPath(activeProjectPath);

        if (string.IsNullOrWhiteSpace(path))
            return normalizedActiveProjectPath;

        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        if (!string.IsNullOrWhiteSpace(normalizedActiveProjectPath))
            return Path.GetFullPath(Path.Combine(normalizedActiveProjectPath, path));

        return null;
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

    private static string? TryGetStringArgument(Microsoft.Extensions.AI.AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value == null) return null;

        return value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            JsonElement json => json.ToString(),
            _ => value.ToString()
        };
    }

    // Records are serialized to JSON and sent to AI - properties accessed via serialization
#pragma warning disable IDE0051, IDE0052, IDE0044, CS9113
    private sealed record DirectoryEntry(string Path, bool IsDirectory, long? SizeBytes);
    private sealed record SearchMatch(string File, int Line, int Column, string LineText, string Match);
#pragma warning restore IDE0051, IDE0052, IDE0044, CS9113
}
