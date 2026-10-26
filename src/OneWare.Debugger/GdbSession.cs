// Lluis Sanchez Gual <lluis@novell.com>
// Copyright (c) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// Substantially rewritten in 2024 by Hendrick Mennen
// Substantially rewritten in 2026 by Daniel Pour Bakhsh

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Helpers;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

// GDB behind IDebugSession. Speaks MI over the standard streams of a
// GDB process and translates between its records and the
// DebugSessionState the UI displays.
// After every halt, frame and registers are read in one go and published as one
// DebugSessionState. The panels therefore fetch nothing themselves,
// and there can be no state in which registers and frame come from different halts.
public class GdbSession(
    string gdbExecutable,
    string? executablePath,
    string? remoteEndpoint,
    string? workingDirectory,
    IReadOnlyList<string>? initCommands,
    bool asyncMode,
    ILogger logger)
    : IDebugSession
{
    // GDB answers every command; waiting longer than this means something hangs.
    private const int CommandTimeout = 10000;

    private readonly string? _elfFile = string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetFileName(executablePath);
    private readonly GdbOutputFormatter _consoleFormatter = new();
    private readonly IReadOnlyList<string> _initCommands = initCommands ?? [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly GdbCommandResult _timeout = new("") { Status = CommandStatus.Timeout };
    private readonly string _workingDir = FirstExistingDirectory(
        string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetDirectoryName(executablePath),
        workingDirectory);

    private bool _clientReady;
    private Process? _process;

    // Register names do not change during a session, so read them once and keep them.
    // Fetching only the values on every halt saves one command per step.
    private IReadOnlyList<string>? _registerNames;

    private volatile TaskCompletionSource<GdbCommandResult>? _pendingCommand;
    private volatile bool _targetRunning;
    private StreamWriter? _writer;
    private StreamReader? _reader;

    // Without a program file GDB starts without an argument. Registers, single steps and the console
    // still work then - only source lines and variables do not, because the symbols are missing.

    // The directory of the program file takes precedence, otherwise that of the project. If neither
    // exists, the current one stays - a missing working directory makes the process start
    // fail, and a missing ELF should not be to blame for that.
    private static string FirstExistingDirectory(params string?[] candidates)
    {
        foreach (var candidate in candidates)
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                return candidate;

        return Directory.GetCurrentDirectory();
    }

    public string BackendId => GdbSessionLauncher.BackendId;

    public DebugSessionState State { get; private set; } = DebugSessionState.Empty;

    public event EventHandler<DebugSessionState>? StateChanged;
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? CommandSent;
    public event EventHandler? Exited;

    public async Task<bool> StartAsync()
    {
        try
        {
            if (!StartProcess() || _process == null) return false;

            _writer = _process.StandardInput;
            _reader = _process.StandardOutput;

            _ = Task.Run(ReadOutput);

            if (_process.HasExited)
            {
                logger.Error("Debugging failed: GDB could not be started.");
                return false;
            }

            _process.ErrorDataReceived += (_, args) => ProcessLine(args.Data);
            _process.Exited += (_, _) =>
            {
                foreach (var text in _consoleFormatter.Flush()) OutputReceived?.Invoke(this, text);

                _clientReady = false;
                _targetRunning = false;
                CompletePending(_timeout);
                Publish(DebugSessionState.Empty);
                Exited?.Invoke(this, EventArgs.Empty);
            };

            if (!await WaitUntilReadyAsync())
            {
                logger.Error("GDB timed out during startup.");
                return false;
            }

            await RunCommandAsync("-gdb-set", "pagination", "off");
            if (asyncMode) await RunCommandAsync("-gdb-set", "mi-async", "on");

            await ApplyInitCommandsAsync();

            return await ConnectRemoteAsync();
        }
        catch (Exception e)
        {
            logger.Error($"GDB at '{gdbExecutable}' failed to start: {e.Message}", e);
            return false;
        }
    }

    public Task RunAsync()
    {
        // An attached target already holds its program and stands at its entry point;
        // GDB learned that on connecting and the UI shows it. Running off here as well
        // takes away exactly the state the user wants to see after the start -
        // registers, memory, current line -, and without a breakpoint set the program runs
        // straight through to the end. It is therefore only started on Continue.
        // -exec-run would be wrong here anyway: it demands a restart the hardware
        // does not offer.
        if (remoteEndpoint != null) return Task.CompletedTask;

        // Without a program and without a target there is nothing to start. -exec-run would only write
        // an error to the console here and give the impression that the start had failed.
        if (_elfFile == null) return Task.CompletedTask;

        return RunCommandAsync("-exec-run");
    }

    public Task ContinueAsync()
    {
        return RunCommandAsync("-exec-continue");
    }

    public async Task PauseAsync()
    {
        if (asyncMode)
        {
            await RunCommandAsync("-exec-interrupt");
            return;
        }

        // Without mi-async GDB accepts no commands while running; halting then only works
        // through a signal to the process. Since the attached target, only local debugging
        // under Windows ends up here - a Ctrl+C from an application without its own
        // console does not always arrive, hence the three attempts.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (_process is not { } process || !_targetRunning) return;

            GdbHelper.SendCtrlC(process.Id);

            if (await WaitUntilStoppedAsync(TimeSpan.FromMilliseconds(500))) return;
        }
    }

    private async Task<bool> WaitUntilStoppedAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (!_targetRunning) return true;

            await Task.Delay(25);
        }

        return !_targetRunning;
    }

    // Without a symbol file there are no source lines. -exec-step fails there with
    // "Cannot find bounds of current function", because it looks for a line that does not exist -
    // stepping then goes by single instructions.
    private bool StepsOverInstructions => _elfFile == null;

    public Task StepIntoAsync()
    {
        return RunCommandAsync(StepsOverInstructions ? "-exec-step-instruction" : "-exec-step");
    }

    public Task StepOverAsync()
    {
        return RunCommandAsync(StepsOverInstructions ? "-exec-next-instruction" : "-exec-next");
    }

    public Task StepOutAsync()
    {
        return RunCommandAsync("-exec-finish");
    }

    public async Task<bool> SetBreakpointAsync(BreakPoint breakpoint)
    {
        var result = await RunCommandAsync("-break-insert", Format(breakpoint));
        return result.Status == CommandStatus.Done;
    }

    public async Task<bool> RemoveBreakpointAsync(BreakPoint breakpoint)
    {
        var result = await RunCommandAsync("clear", Format(breakpoint));

        // "No breakpoint at ..." means the goal is reached - the breakpoint is gone.
        // Reporting that as an error would only produce noise during cleanup.
        return result.Status == CommandStatus.Done ||
               (result.Status == CommandStatus.Error &&
                result.ErrorMessage?.StartsWith("No breakpoint at", StringComparison.OrdinalIgnoreCase) == true);
    }

    public async Task<string?> ReadMemoryAsync(string address, int byteCount)
    {
        if (string.IsNullOrWhiteSpace(address) || byteCount <= 0) return null;

        // -data-read-memory-bytes instead of the deprecated -data-read-memory: it delivers the bytes
        // raw and not sorted into words that would first have to be taken apart again.
        var result = await RunCommandAsync("-data-read-memory-bytes", address, byteCount.ToString());
        if (result.Status != CommandStatus.Done) return null;

        var blocks = result.GetObject("memory");
        if (blocks.Count == 0) return null;

        var contents = blocks.GetObject(0).GetValue("contents");

        return string.IsNullOrEmpty(contents) ? null : FormatBytes(contents);
    }

    public Task<DebugWriteResult> WriteRegisterAsync(string name, ulong value)
    {
        return AssignAsync($"${name}", $"0x{value:X}", "The target refused to change this register.");
    }

    public async Task<DebugWriteResult> WriteMemoryAsync(string address, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(address) || bytes.Length == 0)
            return DebugWriteResult.Failed("There is nothing to write.");

        var result = await RunCommandAsync("-data-write-memory-bytes", Quote(address), Convert.ToHexString(bytes));
        return await ConcludeWriteAsync(result, "The target refused to write to this address.");
    }

    public Task<DebugWriteResult> WriteVariableAsync(string name, long value)
    {
        return AssignAsync(name, value.ToString(CultureInfo.InvariantCulture),
            "The target refused to change this variable.");
    }

    public async Task<string?> EvaluateExpressionAsync(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;

        var result = await RunCommandAsync("-data-evaluate-expression", Quote(expression));
        return result.Status == CommandStatus.Done ? result.GetValue("value") : null;
    }

    private async Task<DebugWriteResult> AssignAsync(string target, string value, string refusal)
    {
        var result = await RunCommandAsync("-data-evaluate-expression", Quote($"{target}={value}"));
        return await ConcludeWriteAsync(result, refusal);
    }

    private async Task<DebugWriteResult> ConcludeWriteAsync(GdbCommandResult result, string refusal)
    {
        if (result.Status != CommandStatus.Done) return DebugWriteResult.Failed(Explain(result, refusal));

        await RepublishHaltAsync();
        return DebugWriteResult.Ok;
    }

    private static string Explain(GdbCommandResult result, string refusal)
    {
        if (string.IsNullOrWhiteSpace(result.ErrorMessage)) return DescribeFailure(result.Status);

        var summary = IsTargetRefusal(result.ErrorMessage) ? refusal : "The value could not be written.";

        return $"{summary}\nGDB: {result.ErrorMessage.Trim()}";
    }

    private static bool IsTargetRefusal(string message)
    {
        return message.Contains("remote failure reply", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Cannot access memory", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RepublishHaltAsync()
    {
        await RunCommandAsync("-interpreter-exec", "console", Quote("maintenance flush register-cache"));

        var frame = await RunCommandAsync("-stack-info-frame");

        Publish(new DebugSessionState
        {
            IsRunning = false,
            CurrentFrame = frame.Status == CommandStatus.Done ? ParseFrame(frame.GetObject("frame")) : State.CurrentFrame,
            Registers = await ReadRegistersAsync(),
            Locals = await ReadLocalsAsync()
        });
    }

    private static string DescribeFailure(CommandStatus status)
    {
        return status switch
        {
            CommandStatus.Running => "The target is running.",
            CommandStatus.Timeout => "GDB did not answer in time.",
            _ => "GDB did not accept the value."
        };
    }

    private static string Quote(string text)
    {
        return $"\"{text.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    public async Task<IReadOnlyList<string>?> GetSourceFilesAsync()                
    {                                                                              
        var result = await RunCommandAsync("-file-list-exec-source-files");        
        if (result.Status != CommandStatus.Done) return null;                      
                                                                                 
        var files = result.GetObject("files");                                     
        var fullnames = new List<string>(files.Count);                             
                                                                                 
        for (var i = 0; i < files.Count; i++)                                      
        {                                                                          
            var entry = files.GetObject(i);
            var file = entry.GetValue("file");
            var fullname = entry.GetValue("fullname");

            // "fullname" has symbolic links resolved, "file" is the path the compiler was given. The
            // editor may know the file under either one, so both are reported.
            if (!string.IsNullOrEmpty(fullname)) fullnames.Add(fullname);
            if (Path.IsPathRooted(file) && file != fullname) fullnames.Add(file);
        }                                                                          
                                                                                 
        return fullnames;                                                          
    }           

    // Turns GDB's hex string into byte pairs separated by spaces. Without the separation it is
    // no longer recognisable where one ends once there are more than a few bytes.
    private static string FormatBytes(string contents)
    {
        var bytes = new List<string>(contents.Length / 2);

        for (var i = 0; i + 1 < contents.Length; i += 2) bytes.Add(contents.Substring(i, 2));

        return string.Join(' ', bytes);
    }

    public async Task SendRawCommandAsync(string command)
    {
        await RunCommandAsync(command);
    }

    // Every step is guarded on its own. If one breaks off, the rest must still
    // run -> otherwise GDB is left behind as an orphan, keeps holding the connection to the target,
    // and the display reports "no session" anyway.
    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                TryInterrupt();

                _ = RunCommandAsync("-gdb-exit", 500);
                _process.WaitForExit(1000);
            }
        }
        catch (Exception e)
        {
            logger.Error($"GDB did not shut down cleanly: {e.Message}", e);
        }

        try
        {
            _writer?.Close();
        }
        catch (Exception e)
        {
            logger.Error($"Could not close GDB's input stream: {e.Message}", e);
        }

        try
        {
            // If GDB stays up after -gdb-exit - for instance because, without mi-async, it is letting the
            // target run and reads no commands -, this is the last resort.
            if (_process is { HasExited: false }) _process.Kill();
        }
        catch (Exception e)
        {
            logger.Error($"Could not kill the GDB process: {e.Message}", e);
        }
        finally
        {
            Publish(DebugSessionState.Empty);
        }
    }

    // Halts a running target so that GDB accepts -gdb-exit at all. With an
    // attached target this goes over the connection, otherwise only through a signal - and that
    // does not exist under Windows. If it fails, Kill takes over.
    private void TryInterrupt()
    {
        if (!_targetRunning) return;

        if (asyncMode)
        {
            _ = RunCommandAsync("-exec-interrupt", 500);
            return;
        }

        if (_process != null) GdbHelper.SendCtrlC(_process.Id);
    }

    // Establishes the connection to the stub. Without an endpoint GDB debugs locally and there is nothing to do.
    private async Task ApplyInitCommandsAsync()
    {
        foreach (var command in _initCommands)
        {
            if (string.IsNullOrWhiteSpace(command)) continue;

            var result = await RunCommandAsync(command);

            if (result.Status is CommandStatus.Done or CommandStatus.Connected) continue;

            logger.Error(
                $"GDB rejected the init command '{command}': {result.ErrorMessage ?? result.Status.ToString()}");
        }
    }

    private async Task<bool> ConnectRemoteAsync()
    {
        if (remoteEndpoint == null) return true;

        var result = await RunCommandAsync("-target-select", "extended-remote", remoteEndpoint);

        // Both ^done and ^connected are valid answers to -target-select according to MI.
        if (result.Status is CommandStatus.Done or CommandStatus.Connected) return true;

        logger.Error(
            $"GDB could not connect to '{remoteEndpoint}': {result.ErrorMessage ?? result.Status.ToString()}");
        return false;
    }

    private async Task<bool> WaitUntilReadyAsync()
    {
        // GDB does not announce that it is ready; the first line it prints is the signal.
        for (var waited = 0; waited < 5000; waited += 100)
        {
            if (_clientReady) return true;
            await Task.Delay(100);
        }

        return _clientReady;
    }

    private bool StartProcess()
    {
        if (!Directory.Exists(_workingDir))
        {
            logger.Error($"Working directory does not exist: '{_workingDir}'");
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = gdbExecutable,
            WorkingDirectory = _workingDir,
            Arguments = BuildArguments(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        try
        {
            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.Start();
            _process.BeginErrorReadLine();
            return true;
        }
        catch (Exception e)
        {
            logger.Error($"Could not start GDB at '{gdbExecutable}': {e.Message}", e);
            return false;
        }
    }

    // Command line for GDB. File names are quoted because otherwise they fall apart into
    // several arguments at every space.
    private string BuildArguments()
    {
        var arguments = new List<string> { "--interpreter=mi" };

        if (_elfFile != null) arguments.Add($"\"{_elfFile}\"");

        return string.Join(' ', arguments);
    }

    private void ReadOutput()
    {
        try
        {
            while (_reader?.ReadLine() is { } line)
            {
                _clientReady = true;
                ProcessLine(line);
            }
        }
        catch (Exception e)
        {
            logger.Error($"Stopped reading GDB's output: {e.Message}", e);
        }
    }

    private void ProcessLine(string? rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine)) return;

        // Task 1: display (prepared instead of raw) ->
        // by contract OutputReceived carries readable text, not MI protocol syntax. Whoever wants to see MI
        // sees it in the records the formatter leaves in place.
        Echo(rawLine);

        // Task 2: evaluate the protocol (raw)
        Dispatch(rawLine.TrimStart());
    }

    private void Echo(string rawLine)
    {
        foreach (var text in _consoleFormatter.Accept(rawLine)) OutputReceived?.Invoke(this, text);
    }

    private void Dispatch(string line)
    {
        if (line.Length == 0) return;

        switch (line[0])
        {
            case '^': // result of a specific GDB request
                var result = new GdbCommandResult(line);
                _targetRunning = result.Status == CommandStatus.Running;
                CompletePending(result);
                break;

            case '*': // async events from GDB
                _targetRunning = line.StartsWith("*running", StringComparison.Ordinal);

                // Do not await: this call runs on the reader thread, which must stay free so that the
                // answers to the commands from HandleEvent arrive at all. -> hence async
                _ = HandleEventAsync(new GdbEvent(line));
                break;
        }
    }

    private async Task HandleEventAsync(GdbEvent gdbEvent)
    {
        try
        {
            switch (gdbEvent.Name)
            {
                case "running":
                    Publish(new DebugSessionState { IsRunning = true });
                    break;

                case "stopped":
                    // Registers and locals in one go, before anything is published:
                    // otherwise one panel already shows the new halt while the other still
                    // holds values from the previous one.
                    Publish(new DebugSessionState
                    {
                        IsRunning = false,
                        CurrentFrame = ParseFrame(gdbEvent.GetObject("frame")),
                        Registers = await ReadRegistersAsync(),
                        Locals = await ReadLocalsAsync()
                    });
                    break;
            }
        }
        catch (Exception e)
        {
            logger.Error($"Could not publish the target state after a stop: {e.Message}", e);
        }
    }

    private async Task<IReadOnlyList<DebugRegisterValue>> ReadRegistersAsync()
    {
        var names = _registerNames ?? await ReadRegisterNamesAsync();
        if (names.Count == 0) return [];

        // Only remember once something really came - a failed first attempt must not
        // lead to registers never being read again for the rest of the session.
        _registerNames = names;
        
        var result = await RunCommandAsync("-data-list-register-values", "r"); // r = raw: hexadecimal, padded to the register width
        if (result.Status != CommandStatus.Done) return [];

        var values = result.GetObject("register-values");
        var registers = new List<DebugRegisterValue>(values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            var entry = values.GetObject(i);

            if (!int.TryParse(entry.GetValue("number"), out var number)) continue;
            if (number < 0 || number >= names.Count) continue;

            // GDB delivers empty names for gaps in the register numbering; they have no
            // place in the display.
            var name = names[number];
            if (string.IsNullOrEmpty(name)) continue;

            registers.Add(new DebugRegisterValue(name, entry.GetValue("value")));
        }

        return registers;
    }

    // Reads the local variables of the current frame. Without symbols GDB reports nothing here -
    // then the panel stays empty, and the registers are all there is to see.
    private async Task<IReadOnlyList<DebugVariable>> ReadLocalsAsync()
    {
        var result = await RunCommandAsync("-stack-list-locals", "2");
        if (result.Status != CommandStatus.Done) return [];

        var locals = result.GetObject("locals");
        var variables = new List<DebugVariable>(locals.Count);

        for (var i = 0; i < locals.Count; i++)
        {
            var entry = locals.GetObject(i);
            var name = entry.GetValue("name");

            if (string.IsNullOrEmpty(name)) continue;

            variables.Add(new DebugVariable(name, entry.GetValue("value"), NullIfEmpty(entry.GetValue("type"))));
        }

        return variables;
    }

    private async Task<IReadOnlyList<string>> ReadRegisterNamesAsync()
    {
        var result = await RunCommandAsync("-data-list-register-names");
        if (result.Status != CommandStatus.Done) return [];

        var names = result.GetObject("register-names");
        var list = new List<string>(names.Count);

        for (var i = 0; i < names.Count; i++) list.Add(names.GetValue(i));

        return list;
    }

    private static DebugStackFrame? ParseFrame(ResultData frame)
    {
        if (frame.Count == 0) return null;

        int.TryParse(frame.GetValue("line"), out var line);

        return new DebugStackFrame(
            NullIfEmpty(frame.GetValue("func")),
            PreferredSourcePath(frame.GetValue("file"), frame.GetValue("fullname")),
            line,
            NullIfEmpty(frame.GetValue("addr")));
    }

    // GDB resolves symbolic links in "fullname". The path the compiler was given ("file") is the one
    // the project and the editor use, so it wins as long as it is absolute and exists.
    private static string? PreferredSourcePath(string file, string fullname)
    {
        if (Path.IsPathRooted(file) && File.Exists(file)) return file;
        return NullIfEmpty(fullname) ?? NullIfEmpty(file);
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private void Publish(DebugSessionState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }
    

    private Task<GdbCommandResult> RunCommandAsync(string command, params string[] args)
    {
        return RunCommandAsync(command, CommandTimeout, args);
    }

    private async Task<GdbCommandResult> RunCommandAsync(string command, int timeout, params string[] args)
    {
        await _semaphore.WaitAsync();

        try
        {
            return await SendAndAwaitAsync(command, timeout, args);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<GdbCommandResult> SendAndAwaitAsync(string command, int timeout, string[] args)
    {
        if (_writer == null) return _timeout;

        if (!asyncMode && _targetRunning)
        {
            OutputReceived?.Invoke(this, "Not possible to run commands while the target is running!");
            return new GdbCommandResult("") { Status = CommandStatus.Running };
        }

        var pending = new TaskCompletionSource<GdbCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingCommand = pending;

        try
        {
            var line = $"{command} {string.Join(" ", args)}".TrimEnd();
            CommandSent?.Invoke(this, line);
            await _writer.WriteLineAsync(line);

            return await pending.Task.WaitAsync(TimeSpan.FromMilliseconds(timeout));
        }
        catch (TimeoutException)
        {
            OutputReceived?.Invoke(this, "error: GDB timed out");
            return _timeout;
        }
        catch (ObjectDisposedException)
        {
            return new GdbCommandResult("") { Status = CommandStatus.Error };
        }
        catch (Exception e)
        {
            logger.Error($"Command '{command}' to GDB failed: {e.Message}", e);
            return _timeout;
        }
        finally
        {
            _pendingCommand = null;
        }
    }

    private void CompletePending(GdbCommandResult result)
    {
        _pendingCommand?.TrySetResult(result);
    }
    
    // Passes file path + line number to GDB in Unix notation
    // -> C:/MyProjects/Test/file.c:lineNr
    private static string Format(BreakPoint breakpoint)
    {
        return $"\"{breakpoint.File.Replace('\\', '/')}:{breakpoint.Line}\"";
    }
}

