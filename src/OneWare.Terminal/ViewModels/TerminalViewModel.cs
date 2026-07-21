using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Terminal.Provider;
using OneWare.Terminal.Provider.Unix;
using OneWare.Terminal.Provider.Win32;
using VtNetCore.Avalonia;
using VtNetCore.VirtualTerminal;

namespace OneWare.Terminal.ViewModels;

public class TerminalViewModel : ObservableObject
{
    private static readonly IPseudoTerminalProvider SProvider = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? new Win32ConPtyPseudoTerminalProvider()
        : new UnixPseudoTerminalProvider();

    private readonly Lock _createLock = new();

    public TerminalViewModel(string workingDir, string? startArguments = null)
    {
        WorkingDir = workingDir;
        StartArguments = startArguments ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? BuildWindowsStartArguments(WorkingDir)
            : null);
    }

    public string? StartArguments { get; }
    public string WorkingDir { get; }

    public IConnection? Connection
    {
        get;
        set => SetProperty(ref field, value);
    }


    public VirtualTerminalController? Terminal
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool TerminalVisible
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool TerminalLoading
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public event EventHandler? TerminalReady;

    public void Redraw()
    {
        if (TerminalVisible)
        {
            TerminalVisible = false;
            TerminalVisible = true;
        }
    }

    public void StartCreate()
    {
        Dispatcher.UIThread.Post(CreateConnection);
    }

    public void CreateConnection()
    {
        if (Connection is { IsConnected: true }) return;
        TerminalLoading = true;

        lock (_createLock)
        {
            CloseConnection();
            
            var shellExecutable = PlatformHelper.Platform switch
            {
                PlatformId.WinX64 or PlatformId.WinArm64 => PlatformHelper.GetFullPath("powershell.exe"),
                PlatformId.LinuxX64 or PlatformId.LinuxArm64 => PlatformHelper.GetFullPath("bash"),
                PlatformId.OsxX64 or PlatformId.OsxArm64 => PlatformHelper.GetFullPath("zsh") ??
                                                            PlatformHelper.GetFullPath("bash"),
                _ => null
            };

            if (!string.IsNullOrEmpty(shellExecutable))
            {
                var environment = BuildTerminalEnvironment(shellExecutable);
                var startArguments = StartArguments;
                var shellName = Path.GetFileName(shellExecutable);

                if (string.IsNullOrWhiteSpace(startArguments) &&
                    shellName.Equals("bash", StringComparison.OrdinalIgnoreCase))
                {
                    // Force an interactive shell and inject the completion marker through a
                    // custom rcfile that sources the user's ~/.bashrc first. Relying only on
                    // the PROMPT_COMMAND environment variable is unreliable because many
                    // ~/.bashrc configurations and prompt frameworks (starship, powerline,
                    // oh-my-bash, ...) overwrite PROMPT_COMMAND. That drops our marker, so
                    // command-completion detection never fires and the terminal hangs until
                    // the timeout expires.
                    var bashRcFile = EnsureBashRcFile();
                    if (!string.IsNullOrWhiteSpace(bashRcFile))
                    {
                        startArguments = $"--rcfile \"{bashRcFile}\" -i";
                    }
                    else
                    {
                        // Fallback when the rcfile cannot be written: still emit the marker via
                        // PROMPT_COMMAND (may be overridden by user config, but better than nothing).
                        environment = BuildBashPromptCommandEnvironment();
                        startArguments = "-i";
                    }
                }
                else if (string.IsNullOrWhiteSpace(startArguments) &&
                         shellName.Equals("zsh", StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure zsh runs interactively so precmd hooks fire.
                    startArguments = "-i";
                }

                var terminal = SProvider.Create(80, 32, WorkingDir, shellExecutable, environment, startArguments);

                if (terminal == null)
                {
                    ContainerLocator.Container.Resolve<ILogger>().Error("Error creating terminal!");
                    return;
                }

                Connection = new PseudoTerminalConnection(terminal);

                Terminal = new VirtualTerminalController();

                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    TerminalVisible = true;
                    Connection.Connect();

                    await Task.Delay(500);

                    TerminalLoading = false;

                    TerminalReady?.Invoke(this, EventArgs.Empty);
                });
            }
        }
    }

    public void Send(string command)
    {
        if (Connection?.IsConnected ?? false) Connection.SendData(Encoding.ASCII.GetBytes($"{command}\r"));
    }

    public void SendInterrupt()
    {
        // Send Ctrl+C (ETX) to abort the currently running foreground command
        // so the shell returns to a usable prompt.
        if (Connection?.IsConnected ?? false) Connection.SendData([0x03]);
    }

    public void KillProcess()
    {
        // Forcibly terminate the shell and any child processes. Used as a last
        // resort when an interrupt (Ctrl+C) fails to free a stuck command.
        if (Connection is PseudoTerminalConnection ptc) ptc.KillProcess();
    }

    public void SuppressEcho(byte[] data)
    {
        if (Connection is IOutputSuppressor suppressor)
        {
            suppressor.SuppressOutput(data);
        }
    }

    public void CloseConnection()
    {
        if (Connection != null)
        {
            Connection.Disconnect();
            Connection = null;
        }
    }

    public void Close()
    {
        CloseConnection();
    }

    private static string BuildWindowsStartArguments(string workingDir)
    {
        // The arguments string must include the full command line because
        // Win32ConPtyPseudoTerminalProvider.BuildCommandLine returns just the arguments when provided
        var escapedDir = workingDir.Replace("'", "''");

        // Keep bootstrap inline so we do not execute external .ps1 files.
        // This avoids requiring users to change PowerShell execution policy.
        var bootstrapCmd =
            "if (Test-Path function:prompt) { $function:__ow_original_prompt = $function:prompt }; " +
            "function global:prompt { " +
            "$esc = [char]27; $bel = [char]7; " +
            "$code = if ($global:LASTEXITCODE -ne $null) { [int]$global:LASTEXITCODE } elseif ($?) { 0 } else { 1 }; " +
            "Write-Host ($esc + ']9;OW_DONE:' + $code + $bel) -NoNewline; " +
            "if (Test-Path function:__ow_original_prompt) { & __ow_original_prompt } else { " +
            "'PS ' + $executionContext.SessionState.Path.CurrentLocation + ('>' * ($nestedPromptLevel + 1)) + ' ' " +
            "} " +
            "}; " +
            $"Set-Location '{escapedDir}'";

        return $"powershell.exe -NoProfile -NoExit -Command \"{bootstrapCmd}\"";
    }

    private static string? BuildTerminalEnvironment(string? shellExecutable)
    {
        if (string.IsNullOrWhiteSpace(shellExecutable)) return null;

        var shellName = Path.GetFileName(shellExecutable);
        
        if (shellName.Equals("bash", StringComparison.OrdinalIgnoreCase))
        {
            // bash uses a custom rcfile (see EnsureBashRcFile) for marker injection, so no
            // environment override is needed here. The PROMPT_COMMAND fallback lives in
            // BuildBashPromptCommandEnvironment and is only used when the rcfile is unavailable.
            return null;
        }

        if (shellName.Equals("zsh", StringComparison.OrdinalIgnoreCase))
        {
            var zshDotDir = EnsureZshDotDir();
            if (string.IsNullOrWhiteSpace(zshDotDir)) return null;

            var overrides = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ZDOTDIR"] = zshDotDir
            };

            return BuildEnvironmentBlock(overrides);
        }

        return null;
    }

    private static string? BuildBashPromptCommandEnvironment()
    {
        var existingPromptCommand = Environment.GetEnvironmentVariable("PROMPT_COMMAND");
        var markerCommand = "printf '\\033]9;OW_DONE:%s\\007' $?";
        var combined = string.IsNullOrWhiteSpace(existingPromptCommand)
            ? markerCommand
            : markerCommand + ";" + existingPromptCommand;

        var overrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PROMPT_COMMAND"] = combined
        };

        return BuildEnvironmentBlock(overrides);
    }

    private static string? EnsureBashRcFile()
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "oneware", "bash");
            Directory.CreateDirectory(tempDir);

            var rcPath = Path.Combine(tempDir, ".ow_bashrc");

            var builder = new StringBuilder();
            builder.AppendLine("# oneware bashrc");
            // Preserve the user's interactive configuration first so their prompt,
            // aliases and functions still apply.
            builder.AppendLine("if [ -f \"$HOME/.bashrc\" ]; then source \"$HOME/.bashrc\"; fi");
            // Append the completion marker AFTER sourcing the user's config so it cannot
            // be clobbered. Prepending our hook keeps $? accurate (it runs before any
            // pre-existing PROMPT_COMMAND can mutate the exit status). Handle both the
            // scalar and array (bash 5.1+) forms of PROMPT_COMMAND.
            builder.AppendLine("__ow_precmd() { printf '\\033]9;OW_DONE:%s\\007' \"$?\"; }");
            builder.AppendLine("case \"$(declare -p PROMPT_COMMAND 2>/dev/null)\" in");
            builder.AppendLine("  \"declare -a\"*) PROMPT_COMMAND=(__ow_precmd \"${PROMPT_COMMAND[@]}\") ;;");
            builder.AppendLine("  *) PROMPT_COMMAND=\"__ow_precmd${PROMPT_COMMAND:+;$PROMPT_COMMAND}\" ;;");
            builder.AppendLine("esac");

            File.WriteAllText(rcPath, builder.ToString(), Encoding.ASCII);
            return rcPath;
        }
        catch
        {
            return null;
        }
    }

    private static string? EnsureZshDotDir()
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "oneware", "zsh");
            Directory.CreateDirectory(tempDir);

            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var baseZdotdir = Environment.GetEnvironmentVariable("ZDOTDIR");
            if (string.IsNullOrWhiteSpace(baseZdotdir)) baseZdotdir = userHome;

            if (!string.IsNullOrWhiteSpace(baseZdotdir))
            {
                WriteZshFile(Path.Combine(tempDir, ".zshenv"), Path.Combine(baseZdotdir, ".zshenv"),
                    "# oneware zshenv");
                WriteZshFile(Path.Combine(tempDir, ".zprofile"), Path.Combine(baseZdotdir, ".zprofile"),
                    "# oneware zprofile");
                WriteZshFile(Path.Combine(tempDir, ".zlogin"), Path.Combine(baseZdotdir, ".zlogin"),
                    "# oneware zlogin");
            }

            var zshrcPath = Path.Combine(tempDir, ".zshrc");
            var zshrcBuilder = new StringBuilder();
            zshrcBuilder.AppendLine("# oneware zshrc");

            var originalZshrc = !string.IsNullOrWhiteSpace(baseZdotdir)
                ? Path.Combine(baseZdotdir, ".zshrc")
                : null;

            if (!string.IsNullOrWhiteSpace(originalZshrc) && File.Exists(originalZshrc))
            {
                zshrcBuilder.Append("source ").Append('"').Append(originalZshrc).Append('"').AppendLine();
            }

            zshrcBuilder.AppendLine("autoload -Uz add-zsh-hook");
            zshrcBuilder.AppendLine("_ow_precmd() { printf '\\033]9;OW_DONE:%s\\007' $?; }");
            zshrcBuilder.AppendLine("add-zsh-hook precmd _ow_precmd");

            File.WriteAllText(zshrcPath, zshrcBuilder.ToString(), Encoding.ASCII);
            return tempDir;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteZshFile(string destinationPath, string sourcePath, string header)
    {
        var builder = new StringBuilder();
        builder.AppendLine(header);
        if (File.Exists(sourcePath))
        {
            builder.Append("source ").Append('"').Append(sourcePath).Append('"').AppendLine();
        }

        File.WriteAllText(destinationPath, builder.ToString(), Encoding.ASCII);
    }

    private static string BuildEnvironmentBlock(Dictionary<string, string> overrides)
    {
        var comparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var env = new SortedDictionary<string, string>(comparer);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string key || entry.Value is not string value) continue;
            env[key] = value;
        }

        foreach (var pair in overrides)
        {
            env[pair.Key] = pair.Value;
        }

        var builder = new StringBuilder();
        foreach (var pair in env)
        {
            builder.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');
        }

        builder.Append('\0');
        return builder.ToString();
    }
}
