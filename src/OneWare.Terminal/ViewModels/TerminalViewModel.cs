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
                if (string.IsNullOrWhiteSpace(startArguments) &&
                    Path.GetFileName(shellExecutable).Equals("zsh", StringComparison.OrdinalIgnoreCase))
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
        
        // Use -NoProfile to prevent default profile, then load our profile which:
        // 1. Loads the user's actual profile
        // 2. Wraps the prompt with completion markers
        var profileCmd = "if ($env:ONEWARE_PS_PROFILE) { $owProfile = Join-Path $env:ONEWARE_PS_PROFILE 'Microsoft.PowerShell_profile.ps1'; if (Test-Path $owProfile) { . $owProfile } }";
        
        return $"powershell.exe -NoProfile -NoExit -Command \"{profileCmd}; Set-Location '{escapedDir}'\"";
    }

    private static string? BuildTerminalEnvironment(string? shellExecutable)
    {
        if (string.IsNullOrWhiteSpace(shellExecutable)) return null;

        var shellName = Path.GetFileName(shellExecutable);
        
        if (shellName.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
            shellName.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase) ||
            shellName.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
        {
            var psProfileDir = EnsurePowerShellProfileDir();
            if (string.IsNullOrWhiteSpace(psProfileDir)) return null;

            var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ONEWARE_PS_PROFILE"] = psProfileDir
            };

            return BuildEnvironmentBlock(overrides);
        }
        
        if (shellName.Equals("bash", StringComparison.OrdinalIgnoreCase))
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
    
    private static string? EnsurePowerShellProfileDir()
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "oneware", "pwsh");
            Directory.CreateDirectory(tempDir);

            var profilePath = Path.Combine(tempDir, "Microsoft.PowerShell_profile.ps1");
            
            var profileBuilder = new StringBuilder();
            profileBuilder.AppendLine("# OneWare PowerShell Profile");
            profileBuilder.AppendLine();
            profileBuilder.AppendLine("# Load user's actual profile if it exists");
            profileBuilder.AppendLine("$userProfile = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'WindowsPowerShell\\Microsoft.PowerShell_profile.ps1'");
            profileBuilder.AppendLine("if (Test-Path $userProfile) {");
            profileBuilder.AppendLine("    try { . $userProfile } catch { Write-Warning \"Error loading profile: $_\" }");
            profileBuilder.AppendLine("}");
            profileBuilder.AppendLine();
            profileBuilder.AppendLine("# Store original prompt function");
            profileBuilder.AppendLine("if (Test-Path function:prompt) {");
            profileBuilder.AppendLine("    $function:__ow_original_prompt = $function:prompt");
            profileBuilder.AppendLine("}");
            profileBuilder.AppendLine();
            profileBuilder.AppendLine("# Override prompt to add completion marker");
            profileBuilder.AppendLine("function global:prompt {");
            profileBuilder.AppendLine("    $esc = [char]27");
            profileBuilder.AppendLine("    $bel = [char]7");
            profileBuilder.AppendLine("    $code = if ($global:LASTEXITCODE) { $global:LASTEXITCODE } elseif ($?) { 0 } else { 1 }");
            profileBuilder.AppendLine("    Write-Host \"$esc]9;OW_DONE:$code$bel\" -NoNewline");
            profileBuilder.AppendLine("    ");
            profileBuilder.AppendLine("    if (Test-Path function:__ow_original_prompt) {");
            profileBuilder.AppendLine("        & __ow_original_prompt");
            profileBuilder.AppendLine("    } else {");
            profileBuilder.AppendLine("        \"PS $($executionContext.SessionState.Path.CurrentLocation)$('>' * ($nestedPromptLevel + 1)) \"");
            profileBuilder.AppendLine("    }");
            profileBuilder.AppendLine("}");

            File.WriteAllText(profilePath, profileBuilder.ToString(), Encoding.UTF8);
            return tempDir;
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
