using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Helpers;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger;

public class GdbSession : IDebugSession
{
    private readonly bool _asyncMode;
    private readonly string _elfFile;
    private readonly object _eventLock = new();
    private readonly object _gdbLock = new();
    private readonly ILogger _logger;
    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly object _syncLock = new();
    private readonly GdbCommandResult _timeout = new("") { Status = CommandStatus.Timeout };
    private readonly string _workingDir;
    private bool _clientReady;
    private CancellationTokenSource? _closeTokenSource;
    private GdbCommandResult? _lastResult;
    private Process? _process;
    private bool _running;
    private StreamWriter? _sIn;
    private StreamReader? _sOut;

    public GdbSession(
        string gdbExecutable,
        string elfFile,
        bool asyncMode,
        ILogger logger,
        IProjectExplorerService projectExplorerService,
        IMainDockService mainDockService)
    {
        GdbExecutable = gdbExecutable;
        ExecutablePath = elfFile;
        _elfFile = Path.GetFileName(elfFile);
        _workingDir = Path.GetDirectoryName(elfFile) ?? throw new InvalidOperationException(nameof(_workingDir));
        _asyncMode = asyncMode;
        _logger = logger;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
    }

    public string GdbExecutable { get; }
    public string ExecutablePath { get; }
    public string AdapterId => "gdb";
    public string DisplayName => "GDB";
    public bool SupportsRawCommand => true;

    public bool IsRunning
    {
        get
        {
            lock (_eventLock)
                return _running;
        }
    }

    public event EventHandler<DebugSessionStateChangedEventArgs>? StateChanged;
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? CommandRun;
    public event EventHandler? Exited;

    public async Task<bool> StartAsync()
    {
        try
        {
            _closeTokenSource = new CancellationTokenSource();

            if (!StartProcess() || _process == null)
                return false;

            _sIn = _process.StandardInput;
            _sOut = _process.StandardOutput;

            _ = Task.Run(OutputInterpreter, _closeTokenSource.Token);

            if (_process.HasExited)
            {
                _logger.Error("Debugging failed: process could not start");
                return false;
            }

            _process.ErrorDataReceived += (_, eventArgs) => ProcessOutput(eventArgs.Data);
            _process.Exited += (_, _) =>
            {
                _clientReady = false;
                Dispatcher.UIThread.Post(() => Exited?.Invoke(this, EventArgs.Empty));
            };

            const int timeout = 5000;
            async Task WaitUntilReadyAsync()
            {
                while (!_clientReady)
                    await Task.Delay(100);
            }

            var readyTask = WaitUntilReadyAsync();
            if (await Task.WhenAny(readyTask, Task.Delay(timeout)) != readyTask)
            {
                _logger.Error("GDB timed out!");
                return false;
            }

            await RunCommandAsync("-enable-pretty-printing");
            if (_asyncMode)
                await RunCommandAsync("-gdb-set", "mi-async", "on");
            await RunCommandAsync("-gdb-set", "pagination", "off");

            return true;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            if (IsRunning)
                Pause();
            RunCommand("-gdb-exit", 500);
        }

        _closeTokenSource?.Cancel();
        _sIn?.Close();
        _process?.Kill();
    }

    public void StartExecution()
    {
        var run = RunCommand("-exec-run");
        if (run.Status == CommandStatus.Error)
            RunCommand("-exec-continue");
    }

    public void Continue()
    {
        RunCommand("-exec-continue");
    }

    public void Pause()
    {
        if (_asyncMode)
        {
            RunCommand("-exec-interrupt");
            return;
        }

        lock (_eventLock)
        {
            var tries = 0;
            const int maxTries = 3;
            do
            {
                if (_process == null)
                    break;

                GdbHelper.SendCtrlC(_process.Id);
                tries++;
                if (tries >= maxTries)
                    break;
                if (!_running)
                    return;
            } while (!Monitor.Wait(_eventLock, 500));
        }
    }

    public void StepInto()
    {
        RunCommand("-exec-step");
    }

    public void StepOver()
    {
        RunCommand("-exec-next");
    }

    public void StepOut()
    {
        RunCommand("-exec-finish");
    }

    public Task ExecuteRawCommandAsync(string command)
    {
        return RunCommandAsync(command);
    }

    public bool InsertBreakpoint(BreakPoint breakpoint)
    {
        return RunCommand("-break-insert " + FormatBreakpoint(breakpoint)).Status == CommandStatus.Done;
    }

    public bool RemoveBreakpoint(BreakPoint breakpoint)
    {
        var result = RunCommand("clear " + FormatBreakpoint(breakpoint));
        return result.Status == CommandStatus.Done ||
               result.Status == CommandStatus.Error &&
               result.ErrorMessage?.StartsWith("No breakpoint at", StringComparison.OrdinalIgnoreCase) == true;
    }

    private bool StartProcess()
    {
        if (!Directory.Exists(_workingDir))
            return false;

        _process = StartSession(_workingDir, GdbExecutable, $"--interpreter=mi {_elfFile}");
        return true;
    }

    private Process StartSession(string workingDirectory, string executable, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        var activeProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        try
        {
            activeProcess.Start();
            activeProcess.BeginErrorReadLine();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return activeProcess;
    }

    private void OutputInterpreter()
    {
        while (_sOut?.ReadLine() is { } line)
        {
            _clientReady = true;
            try
            {
                ProcessOutput(line);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message, ex);
            }
        }
    }

    private void ProcessOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        OutputReceived?.Invoke(this, line);
        line = line.TrimStart();

        switch (line[0])
        {
            case '^':
                lock (_syncLock)
                {
                    _lastResult = new GdbCommandResult(line);
                    lock (_eventLock)
                    {
                        _running = _lastResult.Status == CommandStatus.Running;
                    }

                    Monitor.PulseAll(_syncLock);
                }
                break;

            case '~':
            case '&':
                if (line.Length > 3 && line[1] == '"')
                    line = line.Substring(2, line.Length - 3);
                if (line.StartsWith("Quit", StringComparison.OrdinalIgnoreCase))
                    PublishState(DebugSessionState.Empty);
                break;

            case '*':
                GdbEvent gdbEvent;
                lock (_eventLock)
                {
                    _running = line.StartsWith("*running", StringComparison.Ordinal);
                    gdbEvent = new GdbEvent(line);
                    Monitor.PulseAll(_eventLock);
                }

                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        await HandleEventAsync(gdbEvent);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.Message, ex);
                    }
                }, DispatcherPriority.Input);
                break;

            case '@':
                if (line.Length >= 4)
                    OutputReceived?.Invoke(this, Regex.Unescape(line[2..^1]));
                break;
        }
    }

    private async Task HandleEventAsync(GdbEvent gdbEvent)
    {
        if (gdbEvent.Name == "running")
        {
            PublishState(new DebugSessionState { IsRunning = true });
            return;
        }

        if (gdbEvent.Name != "stopped")
            return;

        var state = await CaptureStoppedStateAsync(gdbEvent);
        PublishState(state);

        var frame = state.CurrentFrame;
        if (!string.IsNullOrWhiteSpace(frame?.FullPath) && File.Exists(frame.FullPath))
        {
            if (await _mainDockService.OpenFileAsync(frame.FullPath) is IEditor editor && frame.Line > 0)
                editor.JumpToLine(frame.Line);
        }
    }

    private async Task<DebugSessionState> CaptureStoppedStateAsync(GdbEvent gdbEvent)
    {
        var callStack = await GetCallStackAsync();
        var locals = await GetLocalsAsync();

        var eventFrame = ParseFrame(gdbEvent.GetObject("frame"));
        var currentFrame = eventFrame ??
                           callStack.FirstOrDefault();

        return new DebugSessionState
        {
            IsRunning = false,
            CurrentFrame = currentFrame,
            CallStack = callStack,
            Locals = locals
        };
    }

    private async Task<IReadOnlyList<DebugStackFrame>> GetCallStackAsync()
    {
        var result = await RunCommandAsync("-stack-list-frames");
        if (result.Status != CommandStatus.Done)
            return Array.Empty<DebugStackFrame>();

        var stack = result.GetObject("stack");
        var frames = new List<DebugStackFrame>();
        for (var i = 0; i < stack.Count; i++)
        {
            var frame = ParseFrame(stack.GetObject(i).GetObject("frame"));
            if (frame != null)
                frames.Add(frame);
        }

        return frames;
    }

    private async Task<IReadOnlyList<DebugVariable>> GetLocalsAsync()
    {
        var result = await RunCommandAsync("-stack-list-locals", "1");
        if (result.Status != CommandStatus.Done)
            return Array.Empty<DebugVariable>();

        var locals = result.GetObject("locals");
        var variables = new List<DebugVariable>();
        for (var i = 0; i < locals.Count; i++)
        {
            var item = locals.GetObject(i);
            variables.Add(new DebugVariable
            {
                Name = item.GetValue("name"),
                Value = item.GetValue("value"),
                TypeName = item.GetValue("type")
            });
        }

        return variables;
    }

    private static DebugStackFrame? ParseFrame(ResultData frame)
    {
        if (frame.Count == 0)
            return null;

        return new DebugStackFrame
        {
            Level = frame.GetInt("level"),
            Address = frame.GetValue("addr"),
            Function = frame.GetValue("func"),
            FileName = frame.GetValue("file"),
            FullPath = frame.GetValue("fullname"),
            Line = frame.GetInt("line")
        };
    }

    private void PublishState(DebugSessionState state)
    {
        StateChanged?.Invoke(this, new DebugSessionStateChangedEventArgs(state));
    }

    public Task<GdbCommandResult> RunCommandAsync(string command, params string[] args)
    {
        return Task.Run(() => RunCommand(command, 10000, args));
    }

    public GdbCommandResult RunCommand(string command, int timeout = 10000, params string[] args)
    {
        lock (_gdbLock)
        {
            lock (_syncLock)
            {
                _lastResult = null;

                if (_sIn == null)
                    return _timeout;

                if (!_asyncMode)
                {
                    lock (_eventLock)
                    {
                        if (_running)
                        {
                            OutputReceived?.Invoke(this, "Not possible to run commands while the target is running!");
                            return new GdbCommandResult("Not possible while the target is running")
                            {
                                Status = CommandStatus.Running
                            };
                        }

                        _running = true;
                    }
                }

                try
                {
                    var cmd = $" {command} {string.Join(" ", args)}".TrimEnd();
                    CommandRun?.Invoke(this, cmd);
                    _sIn.WriteLine(cmd);

                    if (!Monitor.Wait(_syncLock, timeout))
                    {
                        _lastResult = new GdbCommandResult("") { Status = CommandStatus.Timeout };
                        OutputReceived?.Invoke(this, "^error, GDB timed out");
                    }

                    return _lastResult ?? _timeout;
                }
                catch (ObjectDisposedException)
                {
                    return new GdbCommandResult("") { Status = CommandStatus.Error };
                }
                catch (Exception e)
                {
                    _logger.Error(e.Message, e);
                    return _lastResult ?? _timeout;
                }
            }
        }
    }

    private static string FormatBreakpoint(BreakPoint breakpoint)
    {
        return $"\"{breakpoint.File.Replace('\\', '/')}:{breakpoint.Line}\"";
    }
}
