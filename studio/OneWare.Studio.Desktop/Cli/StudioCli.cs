using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OneWare.Core.Services;

namespace OneWare.Studio.Desktop.Cli;

/// <summary>
/// Hosts the headless CLI commands inside the Studio executable.
/// Invocations without a subcommand (no args, a path or a oneware:// URI) continue to start the GUI.
/// </summary>
internal static class StudioCli
{
    private const string ConsoleAliasName = "oneware";

    private static readonly HashSet<string> HelpOrVersionTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "-h", "--help", "-?", "/?", "/h", "--version"
    };

    /// <summary>
    /// True when running as the Windows console-subsystem alias (oneware.exe) of OneWareStudio.exe.
    /// </summary>
    public static bool IsWindowsConsoleAlias =>
        OperatingSystem.IsWindows() &&
        string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), ConsoleAliasName,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decides without loading any modules whether the arguments target a CLI command.
    /// The first positional token is treated as a command if it looks like a command name and is not an
    /// existing file or directory. Help and version requests are always handled by the CLI.
    /// </summary>
    public static bool IsCliInvocation(IReadOnlyList<string> args)
    {
        if (args.Any(HelpOrVersionTokens.Contains))
            return true;

        var symbols = OneWareStartupCommandLine.CreateSymbols();
        var valueOptions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in OneWareStartupCommandLine.CreateRootCommand(symbols).Options)
        {
            valueOptions.Add(option.Name);
            foreach (var alias in option.Aliases)
                valueOptions.Add(alias);
        }

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];

            if (token == "--")
                return false;

            if (token.StartsWith('-'))
            {
                if (valueOptions.Contains(token))
                    i++;
                continue;
            }

            return LooksLikeCommandName(token);
        }

        return false;
    }

    public static int Run(string[] args)
    {
        if (OperatingSystem.IsWindows())
            AttachToParentConsole();

        try
        {
            // Apply startup options first, so paths (appdata, plugins, settings) resolve like in the GUI.
            var bootstrapSymbols = OneWareStartupCommandLine.CreateSymbols();
            var bootstrapCommand = OneWareStartupCommandLine.CreateRootCommand(bootstrapSymbols);
            bootstrapCommand.TreatUnmatchedTokensAsErrors = false;
            OneWareStartupCommandLine.ApplyEnvironmentVariables(bootstrapCommand.Parse(args), bootstrapSymbols);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var symbols = OneWareStartupCommandLine.CreateSymbols(
            "File, folder or oneware:// URI to open. Starts OneWare Studio when no command is given.");
        var rootCommand = OneWareStartupCommandLine.CreateRootCommand(symbols);
        rootCommand.Description = "OneWare Studio. Run without a command to start the IDE.";

        var studioProcessController = new StudioProcessController();
        var cliHostBuilder = CliHostFactory.Create();
        var cliModuleLoader = new CliModuleLoader(cliHostBuilder);

        cliModuleLoader.RegisterBuiltInCliModules(studioProcessController.StopStudio);
        if (!ExtensionStoreCliModule.IsExtensionStoreCommand(args))
            cliModuleLoader.LoadPluginCliModules();

        using var cliHost = cliHostBuilder.Build();
        foreach (var command in cliHost.ModuleManager.RegisterCommands(cliHost.ServiceProvider))
            rootCommand.Subcommands.Add(command);

        // Only reached when the first token looked like a command but no command matched it.
        rootCommand.SetAction(parseResult =>
        {
            var token = parseResult.GetValue(symbols.OpenArgument);
            Console.Error.WriteLine(
                $"'{token}' is not a OneWare command or an existing file. Run '{ConsoleAliasName} --help' for usage.");
            return 1;
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static bool LooksLikeCommandName(string token)
    {
        return token.Length > 0 &&
               char.IsLetter(token[0]) &&
               token.All(c => char.IsLetterOrDigit(c) || c is '-' or '_') &&
               !File.Exists(token) &&
               !Directory.Exists(token);
    }

    private static void AttachToParentConsole()
    {
        // OneWareStudio.exe is a GUI-subsystem app. When started from a terminal, attach to it so output is visible.
        // oneware.exe (console subsystem) already has a console.
        // Output that is already redirected (pipes, files, IDE runners) must keep its handles.
        if (GetConsoleWindow() == IntPtr.Zero && GetStdHandle(StdOutputHandle) == IntPtr.Zero)
            AttachConsole(AttachParentProcess);
    }

    private const int AttachParentProcess = -1;
    private const int StdOutputHandle = -11;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int stdHandle);
}
