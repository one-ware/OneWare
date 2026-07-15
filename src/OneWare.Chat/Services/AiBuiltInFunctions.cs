using System.ComponentModel;
using System.Text.Json;
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

    private static readonly TimeSpan TerminalCommandTimeout = TimeSpan.FromHours(12);

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
            Name = "readFile",
            FriendlyName = "Read File",
            RunOnUiThread = true,
            Description = "Read the specified file (optionally by line range). Always pass an absolute path.",
            Handler = ([Description("absolute path of the file to read")] string path,
                    [Description("1-based start line for partial reads (omit for full file)")] int? startLine = null,
                    [Description("number of lines to read from startLine (omit for full file)")] int? lineCount =
                        null) =>
                ReadFileAsync(projectExplorerService, aiFileEditService, path, startLine, lineCount),
            DetailExtractor = args => GetRelativePath(projectExplorerService, TryGetStringArgument(args, "path")),
            ConfirmationCheck = args =>
            {
                var rawPath = TryGetStringArgument(args, "path");
                var resolved = ResolvePath(projectExplorerService, rawPath);
                if (IsInsideWorkspace(projectExplorerService, resolved)) return null;
                return $"**Copilot wants to read a file outside the workspace.**\n\n`{resolved ?? rawPath ?? "?"}`";
            }
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "editFile",
            FriendlyName = "Edit File",
            RunOnUiThread = true,
            Description =
                "Edit file contents with new text (optionally by line range). Creates missing files automatically. Always pass an absolute path.",
            Handler = ([Description("absolute path of the file to edit")] string path,
                    [Description("new text to write (full file or replacement lines)")] string content,
                    [Description("1-based start line for partial edits (omit for full file)")] int? startLine = null,
                    [Description("number of lines to replace from startLine; 0 inserts before startLine")] int?
                        lineCount = null) =>
                EditFileAsync(projectExplorerService, aiFileEditService, path, content, startLine, lineCount),
            DetailExtractor = args => GetRelativePath(projectExplorerService, TryGetStringArgument(args, "path")),
            ConfirmationCheck = args =>
            {
                var rawPath = TryGetStringArgument(args, "path");
                var resolved = ResolvePath(projectExplorerService, rawPath);
                if (IsInsideWorkspace(projectExplorerService, resolved)) return null;
                return $"**Copilot wants to edit a file outside the workspace.**\n\n`{resolved ?? rawPath ?? "?"}`";
            }
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
            },
            DetailExtractor = _ => "active project"
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
            },
            DetailExtractor = _ => "open files"
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
            },
            DetailExtractor = _ => GetRelativePath(projectExplorerService, dockService.CurrentDocument?.FullPath)
                                   ?? "focused file"
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "getErrorsForFile",
            FriendlyName = "Get Errors for File",
            RunOnUiThread = true,
            Description = "Returns the LSP Errors for the specified path (if any)",
            Handler = ([Description("absolute path of the file to get errors")] string path) =>
                GetErrorsForFile(projectExplorerService, errorService, path),
            DetailExtractor = args => GetRelativePath(projectExplorerService, TryGetStringArgument(args, "path"))
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
            },
            DetailExtractor = _ => "all errors"
        });

        functionProvider.RegisterFunction(new OneWareAiFunction
        {
            Name = "runTerminalCommand",
            FriendlyName = "Execute In Terminal",
            RunOnUiThread = true,
            Description = """
                          Executes a command in the IDE terminal and returns the output.
                          Use this to run shell commands; output appears in the IDE terminal panel.
                          Output is automatically truncated to avoid oversized responses.
                          """,
            Handler = ([Description("Shell command to execute")] string command,
                    [Description("Absolute working directory for execution (optional, defaults to active project).")]
                    string? workDir = null,
                    CancellationToken cancellationToken = default) =>
                RunTerminalCommandAsync(projectExplorerService, terminalManagerService, command, workDir,
                    cancellationToken),
            DetailExtractor = args => TryGetStringArgument(args, "command"),
            ConfirmationCheck = args =>
            {
                var cmd = TryGetStringArgument(args, "command") ?? "?";
                return $"**Copilot wants to execute a command in the terminal.**\n\n```\n{cmd}\n```";
            }
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
            },
            DetailExtractor = _ => "IDE settings"
        });
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
        string? workDir,
        CancellationToken cancellationToken)
    {
        var resolvedWorkDir = ResolvePath(projectExplorerService, workDir);

        var terminalResult = await terminalManagerService.ExecuteInTerminalAsync(
            command,
            "AI Chat",
            resolvedWorkDir,
            true,
            TerminalCommandTimeout,
            cancellationToken);

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

    private static string? GetRelativePath(IProjectExplorerService projectExplorerService, string? rawPath)
    {
        var resolved = ResolvePath(projectExplorerService, rawPath);
        if (string.IsNullOrWhiteSpace(resolved)) return rawPath;

        var projectRoot = projectExplorerService.ActiveProject?.FullPath;
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            try
            {
                var rel = Path.GetRelativePath(Path.GetFullPath(projectRoot), resolved);
                if (!rel.StartsWith("..")) return rel;
            }
            catch { }
        }

        return Path.GetFileName(resolved);
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

    /// <summary>
    /// Returns true when <paramref name="resolvedPath"/> is under the active project root,
    /// meaning no confirmation is needed for that path.
    /// </summary>
    private static bool IsInsideWorkspace(IProjectExplorerService projectExplorerService, string? resolvedPath)
    {
        if (string.IsNullOrEmpty(resolvedPath)) return false;

        var projectRoot = projectExplorerService.ActiveProject?.FullPath;
        if (string.IsNullOrEmpty(projectRoot)) return false;

        var normalizedRoot = Path.GetFullPath(projectRoot);
        var normalizedPath = Path.GetFullPath(resolvedPath);

        return normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase);
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
}
