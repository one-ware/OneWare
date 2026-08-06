// Der Prozes

//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
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

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Helpers;
using OneWare.Essentials.Debugger;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

/// <summary>
///     GDB hinter <see cref="IDebugSession" />. Spricht MI ueber die Standardstroeme eines
///     GDB-Prozesses und uebersetzt zwischen dessen Records und dem
///     <see cref="DebugSessionState" />, den die Oberflaeche anzeigt.
/// </summary>
/// <remarks>
///     Nach jedem Halt werden Frame und Register in einem Rutsch gelesen und als ein
///     <see cref="DebugSessionState" /> veroeffentlicht. Die Panels ziehen sich also nichts selbst,
///     und es kann keinen Zustand geben, in dem Register und Frame aus verschiedenen Halts stammen.
/// </remarks>
public class GdbSession : IDebugSession
{
    /// <summary>
    ///     GDB antwortet auf jedes Kommando; laenger als das zu warten heisst, dass etwas haengt.
    /// </summary>
    private const int CommandTimeout = 10000;

    private readonly bool _asyncMode;
    private readonly string? _elfFile;
    private readonly object _eventLock = new();
    private readonly string _gdbExecutable;
    private readonly object _gdbLock = new();
    private readonly ILogger _logger;
    private readonly string? _remoteEndpoint;
    private readonly object _syncLock = new();
    private readonly GdbCommandResult _timeout = new("") { Status = CommandStatus.Timeout };
    private readonly string _workingDir;

    private bool _clientReady;
    private GdbCommandResult? _lastResult;
    private Process? _process;

    /// <summary>
    ///     Registernamen aendern sich waehrend einer Session nicht, also einmal lesen und behalten.
    ///     Bei jedem Halt nur noch die Werte zu holen spart pro Schritt ein Kommando.
    /// </summary>
    private IReadOnlyList<string>? _registerNames;

    private bool _running;
    private StreamWriter? _sIn;
    private StreamReader? _sOut;

    public GdbSession(string gdbExecutable, string? executablePath, string? remoteEndpoint,
        string? workingDirectory, bool asyncMode, ILogger logger)
    {
        _gdbExecutable = gdbExecutable;
        _remoteEndpoint = remoteEndpoint;
        _asyncMode = asyncMode;
        _logger = logger;

        // Ohne Programmdatei startet GDB ohne Argument. Register, Einzelschritte und die Console
        // gehen dann trotzdem - nur Quellzeilen und Variablen nicht, weil die Symbole fehlen.
        _elfFile = string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetFileName(executablePath);

        _workingDir = FirstExistingDirectory(
            string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetDirectoryName(executablePath),
            workingDirectory);
    }

    /// <summary>
    ///     Das Verzeichnis der Programmdatei hat Vorrang, sonst das des Projekts. Existiert keines
    ///     von beiden, bleibt das aktuelle - ein nicht vorhandenes Arbeitsverzeichnis laesst den
    ///     Prozessstart scheitern, und daran soll ein fehlendes ELF nicht schuld sein.
    /// </summary>
    private static string FirstExistingDirectory(params string?[] candidates)
    {
        foreach (var candidate in candidates)
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                return candidate;

        return Directory.GetCurrentDirectory();
    }

    public string AdapterId => GdbDebugAdapter.AdapterId;

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

            _sIn = _process.StandardInput;
            _sOut = _process.StandardOutput;

            _ = Task.Run(ReadOutput);

            if (_process.HasExited)
            {
                _logger.Error("Debugging failed: GDB could not be started.");
                return false;
            }

            _process.ErrorDataReceived += (_, args) => ProcessLine(args.Data);
            _process.Exited += (_, _) =>
            {
                _clientReady = false;
                Publish(DebugSessionState.Empty);
                Exited?.Invoke(this, EventArgs.Empty);
            };

            if (!await WaitUntilReadyAsync())
            {
                _logger.Error("GDB timed out during startup.");
                return false;
            }

            await RunCommandAsync("-gdb-set", "pagination", "off");
            if (_asyncMode) await RunCommandAsync("-gdb-set", "mi-async", "on");

            return await ConnectRemoteAsync();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }
    }

    public async Task RunAsync()
    {
        // Ohne Programm und ohne Ziel gibt es nichts zu starten. -exec-run wuerde hier nur einen
        // Fehler in die Console schreiben und den Eindruck erwecken, der Start sei fehlgeschlagen.
        if (_elfFile == null && _remoteEndpoint == null) return;

        // Ein angehaengtes Ziel haelt sein Programm schon und laeuft nur noch nicht - dort ist
        // -exec-run falsch, weil es einen Neustart verlangt, den die Hardware nicht anbietet.
        await RunCommandAsync(_remoteEndpoint == null ? "-exec-run" : "-exec-continue");
    }

    public Task ContinueAsync()
    {
        return RunCommandAsync("-exec-continue");
    }

    public async Task PauseAsync()
    {
        if (_asyncMode)
        {
            await RunCommandAsync("-exec-interrupt");
            return;
        }

        // Ohne mi-async nimmt GDB waehrend des Laufs keine Kommandos an; das Anhalten geht dann
        // nur ueber ein Signal an den Prozess.
        await Task.Run(() =>
        {
            lock (_eventLock)
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    if (_process == null) return;
                    if (!_running) return;

                    GdbHelper.SendCtrlC(_process.Id);
                    if (Monitor.Wait(_eventLock, 500)) return;
                }
            }
        });
    }

    /// <summary>
    ///     Ohne Symboldatei gibt es keine Quellzeilen. <c>-exec-step</c> scheitert dort mit
    ///     "Cannot find bounds of current function", weil es eine Zeile sucht, die es nicht gibt -
    ///     geschritten wird dann ueber einzelne Instruktionen.
    /// </summary>
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

        // "No breakpoint at ..." heisst, dass das Ziel erreicht ist - der Breakpoint ist weg.
        // Das als Fehler zu melden wuerde beim Aufraeumen nur Rauschen erzeugen.
        return result.Status == CommandStatus.Done ||
               (result.Status == CommandStatus.Error &&
                result.ErrorMessage?.StartsWith("No breakpoint at", StringComparison.OrdinalIgnoreCase) == true);
    }

    public async Task<string?> ReadMemoryAsync(string address, int byteCount)
    {
        if (string.IsNullOrWhiteSpace(address) || byteCount <= 0) return null;

        // -data-read-memory-bytes statt des veralteten -data-read-memory: es liefert die Bytes
        // roh und nicht in Woerter einsortiert, die man erst wieder auseinandernehmen muesste.
        var result = await RunCommandAsync("-data-read-memory-bytes", address, byteCount.ToString());
        if (result.Status != CommandStatus.Done) return null;

        var blocks = result.GetObject("memory");
        if (blocks.Count == 0) return null;

        var contents = blocks.GetObject(0).GetValue("contents");

        return string.IsNullOrEmpty(contents) ? null : FormatBytes(contents);
    }

    /// <summary>
    ///     Macht aus der Hex-Kette von GDB Bytepaare mit Leerzeichen. Ohne die Trennung ist bei
    ///     mehr als ein paar Bytes nicht mehr zu erkennen, wo eines aufhoert.
    /// </summary>
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

    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                if (_running) GdbHelper.SendCtrlC(_process.Id);
                RunCommand("-gdb-exit", 500);
            }

            _sIn?.Close();

            if (_process is { HasExited: false }) _process.Kill();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
        finally
        {
            Publish(DebugSessionState.Empty);
        }
    }

    /// <summary>
    ///     Baut die Verbindung zum Stub auf. Ohne Endpunkt debuggt GDB lokal und es ist nichts zu tun.
    /// </summary>
    private async Task<bool> ConnectRemoteAsync()
    {
        if (_remoteEndpoint == null) return true;

        var result = await RunCommandAsync("-target-select", "extended-remote", _remoteEndpoint);

        // Sowohl ^done als auch ^connected sind laut MI gueltige Antworten auf -target-select.
        if (result.Status is CommandStatus.Done or CommandStatus.Connected) return true;

        _logger.Error($"GDB could not connect to '{_remoteEndpoint}': {result.ErrorMessage ?? result.Status.ToString()}");
        return false;
    }

    private async Task<bool> WaitUntilReadyAsync()
    {
        // GDB meldet seine Bereitschaft nicht; die erste ausgegebene Zeile ist das Signal.
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
            _logger.Error($"Working directory does not exist: '{_workingDir}'");
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _gdbExecutable,
            WorkingDirectory = _workingDir,
            Arguments = _elfFile == null ? "--interpreter=mi" : $"--interpreter=mi {_elfFile}",
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
            _logger.Error($"Could not start GDB at '{_gdbExecutable}': {e.Message}", e);
            return false;
        }
    }

    private void ReadOutput()
    {
        try
        {
            while (_sOut?.ReadLine() is { } line)
            {
                _clientReady = true;
                ProcessLine(line);
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    private void ProcessLine(string? rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine)) return;

        // Aufbereitet statt roh: laut Vertrag traegt OutputReceived lesbaren Text, keine
        // Protokollsyntax. Wer MI sehen will, sieht es an den Records, die der Formatter stehen laesst.
        if (GdbOutputFormatter.Format(rawLine) is { } formatted) OutputReceived?.Invoke(this, formatted);

        var line = rawLine.TrimStart();
        if (line.Length == 0) return;

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

            case '*':
                GdbEvent gdbEvent;
                lock (_eventLock)
                {
                    _running = line.StartsWith("*running", StringComparison.Ordinal);
                    gdbEvent = new GdbEvent(line);
                    Monitor.PulseAll(_eventLock);
                }

                // Nicht abwarten: dieser Aufruf laeuft auf dem Lesethread, der frei bleiben muss,
                // damit die Antworten der Kommandos aus HandleEvent ueberhaupt ankommen.
                _ = HandleEventAsync(gdbEvent);
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
                    // Register und Locals in einem Rutsch, bevor irgendetwas veroeffentlicht wird:
                    // sonst zeigt das eine Panel schon den neuen Halt, waehrend das andere noch
                    // Werte vom vorigen stehen hat.
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
            _logger.Error(e.Message, e);
        }
    }

    private async Task<IReadOnlyList<RegisterValue>> ReadRegistersAsync()
    {
        var names = _registerNames ?? await ReadRegisterNamesAsync();
        if (names.Count == 0) return [];

        // Erst merken, wenn wirklich etwas kam - ein fehlgeschlagener erster Versuch darf nicht
        // dazu fuehren, dass fuer den Rest der Session nie wieder Register gelesen werden.
        _registerNames = names;

        // x = hexadezimal. Register sind Bitmuster; dezimal gelesen sagen sie fast nie etwas.
        var result = await RunCommandAsync("-data-list-register-values", "x");
        if (result.Status != CommandStatus.Done) return [];

        var values = result.GetObject("register-values");
        var registers = new List<RegisterValue>(values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            var entry = values.GetObject(i);

            if (!int.TryParse(entry.GetValue("number"), out var number)) continue;
            if (number < 0 || number >= names.Count) continue;

            // GDB liefert fuer Luecken in der Registernummerierung leere Namen; die haben in der
            // Anzeige nichts zu suchen.
            var name = names[number];
            if (string.IsNullOrEmpty(name)) continue;

            registers.Add(new RegisterValue(name, entry.GetValue("value")));
        }

        return registers;
    }

    /// <summary>
    ///     Liest die lokalen Variablen des aktuellen Frames. Ohne Symbole meldet GDB hier nichts -
    ///     dann bleibt das Panel leer, und die Register sind alles, was es zu sehen gibt.
    /// </summary>
    private async Task<IReadOnlyList<DebugVariable>> ReadLocalsAsync()
    {
        // Das Argument 1 heisst "mit Werten", nicht nur mit Namen.
        var result = await RunCommandAsync("-stack-list-locals", "1");
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
            NullIfEmpty(frame.GetValue("fullname")),
            line,
            NullIfEmpty(frame.GetValue("addr")));
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

    private static string Format(BreakPoint breakpoint)
    {
        return $"\"{breakpoint.File.Replace('\\', '/')}:{breakpoint.Line}\"";
    }

    private Task<GdbCommandResult> RunCommandAsync(string command, params string[] args)
    {
        // Task.Run, weil RunCommand blockierend auf die Antwort wartet. Ohne das wuerde jeder
        // Schritt den UI-Thread bis zu CommandTimeout lang einfrieren.
        return Task.Run(() => RunCommand(command, CommandTimeout, args));
    }

    private GdbCommandResult RunCommand(string command, int timeout, params string[] args)
    {
        lock (_gdbLock)
        lock (_syncLock)
        {
            _lastResult = null;

            if (_sIn == null) return _timeout;

            if (!_asyncMode)
                lock (_eventLock)
                {
                    if (_running)
                    {
                        OutputReceived?.Invoke(this, "Not possible to run commands while the target is running!");
                        return new GdbCommandResult("") { Status = CommandStatus.Running };
                    }

                    _running = true;
                }

            try
            {
                var line = $"{command} {string.Join(" ", args)}".TrimEnd();
                CommandSent?.Invoke(this, line);
                _sIn.WriteLine(line);

                if (!Monitor.Wait(_syncLock, timeout))
                {
                    OutputReceived?.Invoke(this, "error: GDB timed out");
                    return _timeout;
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
