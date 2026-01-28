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

    private readonly object _createLock = new();

    private IConnection? _connection;

    private VirtualTerminalController? _terminal;

    private bool _terminalLoading = true;

    private bool _terminalVisible;

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
        get => _connection;
        set => SetProperty(ref _connection, value);
    }


    public VirtualTerminalController? Terminal
    {
        get => _terminal;
        set => SetProperty(ref _terminal, value);
    }

    public bool TerminalVisible
    {
        get => _terminalVisible;
        set => SetProperty(ref _terminalVisible, value);
    }

    public bool TerminalLoading
    {
        get => _terminalLoading;
        set => SetProperty(ref _terminalLoading, value);
    }

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

            //TODO Fix zsh support
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
                var terminal = SProvider.Create(80, 32, WorkingDir, shellExecutable, environment, StartArguments);

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
        var marker = "$esc=[char]27; $bel=[char]7; " +
                     "$code = if ($LASTEXITCODE -ne $null -and $LASTEXITCODE -ne 0) { $LASTEXITCODE } " +
                     "elseif ($?) { 0 } else { 1 }; " +
                     "Write-Host \"$esc]9;OW_DONE:$code$bel\" -NoNewline;";
        var prompt = "function global:prompt { " + marker + " \"PS $PWD> \" }";

        return $"powershell.exe -NoExit Set-Location '{workingDir}'; {prompt}";
    }

    private static string? BuildTerminalEnvironment(string? shellExecutable)
    {
        if (string.IsNullOrWhiteSpace(shellExecutable)) return null;

        var shellName = Path.GetFileName(shellExecutable);
        if (!shellName.Equals("bash", StringComparison.OrdinalIgnoreCase)) return null;

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
