// GdbSession.cs
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
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Helpers;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger;

public abstract class GdbSession(
    string gdbExecutable,
    string elfFile,
    bool asyncMode,
    ILogger logger,
    IProjectExplorerService projectExplorerService,
    IDockService dockService)
{
    private readonly string _elfFile = Path.GetFileName(elfFile);
    private readonly object _eventLock = new();
    private readonly object _gdbLock = new();
    private readonly object _syncLock = new();
    private readonly GdbCommandResult _timeout = new("") { Status = CommandStatus.Timeout };
    private readonly string _workingDir = Path.GetDirectoryName(elfFile) ?? throw new Exception(nameof(_workingDir));
    private bool _clientReady;

    private CancellationTokenSource? _closeTokenSource;
    private GdbCommandResult? _lastResult;

    private Process? _process;

    private bool _running;

    private StreamWriter? _sIn;
    private StreamReader? _sOut;

    public event EventHandler<GdbEventArgs>? EventFired;

    public event EventHandler<string>? OutputReceived;

    public event EventHandler<string>? CommandRun;

    public event EventHandler? Exited;

    protected virtual bool StartProcess()
    {
        if (!Directory.Exists(_workingDir)) return false;

        _process = StartSession(_workingDir, gdbExecutable, $"--interpreter=mi {_elfFile}");
        return true;
    }

    public async Task<bool> RunAsync(bool download)
    {
        try
        {
            _closeTokenSource = new CancellationTokenSource();

            if (!StartProcess() || _process == null) return false;

            //_asyncMode = !Global.Options.WslNios && RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            _sIn = _process.StandardInput;
            _sOut = _process.StandardOutput;

            _ = Task.Run(OutputInterpreter, _closeTokenSource.Token);

            if (_process == null || _process.HasExited)
            {
                logger.LogError("Debugging failed: process could not start");
                return false;
            }

            _process.ErrorDataReceived += (_, i) => ProcessOutput(i.Data);

            _process.Exited += (_, _) =>
            {
                _clientReady = false;
                Dispatcher.UIThread.Post(() => { Exited?.Invoke(this, EventArgs.Empty); });
            };

            const int timeout = 5000;

            async Task WhenReadyAsync()
            {
                while (!_clientReady) await Task.Delay(100);
            }

            var task = WhenReadyAsync();

            if (await Task.WhenAny(task, Task.Delay(timeout)) != task)
            {
                // task completed after timeout
                logger.LogError("GDB timed out!");
                return false;
            }

            await RunCommandAsync("-enable-pretty-printing");

            if (asyncMode) await RunCommandAsync("-gdb-set", "mi-async", "on"); //Async mode

            await RunCommandAsync("-gdb-set", "pagination", "off");

            await SetupSettingsAsync();

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return false;
        }
    }

    protected virtual Task SetupSettingsAsync()
    {
        return Task.CompletedTask;
    }

    public async Task SetSettingsAsync(params string[] settings)
    {
        foreach (var setting in settings) await RunCommandAsync(setting);
    }

    protected Process StartSession(string workingDirectory, string executable, string arguments)
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
            logger.LogError(e, e.Message);
        }

        return activeProcess;
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            if (_running) Pause();
            RunCommand("-gdb-exit", 500);
        }

        _closeTokenSource?.Cancel();
        _sIn?.Close();
        _process?.Kill();
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
                logger.LogError(ex, ex.Message);
            }
        }
    }

    private void ProcessOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

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

                if (line.Length > 3 && line[1] == '"') line = line.Substring(2, line.Length - 3);
                _ = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (line.StartsWith("Quit", StringComparison.OrdinalIgnoreCase)) _running = false;
                    //MainDock.DebuggerLocals.Refresh(await RunCommandAsync("-stack-list-locals 1"));
                    //MainDock.DebuggerCallStack.Refresh(await RunCommandAsync("-stack-list-frames"));
                });
                break;

            case '*':
                GdbEvent? ev;
                lock (_eventLock)
                {
                    _running = line.StartsWith("*running");
                    ev = new GdbEvent(line);
                    Monitor.PulseAll(_eventLock);
                }

                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        await HandleEventAsync(ev);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, ex.Message);
                    }
                }, DispatcherPriority.Input);

                break;
            case '@':
                if (line.Length >= 4)
                    //Serial output
                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var lineS = Regex.Unescape(line[2..^1]);
                        //MainDock.SerialMonitor.Write(lineS);
                    });
                break;
        }
    }

    private async Task HandleEventAsync(GdbEvent ev)
    {
        EventFired?.Invoke(this, new GdbEventArgs(ev));
        if (ev.Name != "stopped") return;

        var frame = ev.GetObject("frame");
        var fullPath = frame.GetValue("fullname");
        if (!int.TryParse(frame.GetValue("line"), out var lineNr)) return;
        if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
        {
            var file = projectExplorerService.SearchFullPath(fullPath) as IFile ??
                       projectExplorerService.GetTemporaryFile(fullPath);

            var evm = await dockService.OpenFileAsync(file) as IEditor;

            evm?.JumpToLine(lineNr);

            // Global.Breakpoints.CurrentBreakPoint =
            //     Global.Breakpoints.Breakpoints.FirstOrDefault(x =>
            //         x.File.EqualPaths(fullPath) && x.Line == lineNr) ?? new BreakPoint
            //         { File = fullPath, Line = lineNr };
            //
            // //JUMP TO LINE
            // if (Global.Options.DebuggerJumpToBreak)
            //     if (lineNr > 0
            //         && await MainDock.AddTabAsync(file) is EditViewModel evb
            //         && lineNr <= evb.CurrentDocument.LineCount)
            //         evb.JumpToLine(lineNr);


            //MainDock.DebuggerLocals.Refresh(await RunCommandAsync("-stack-list-locals 1"));
            //MainDock.DebuggerCallStack.Refresh(await RunCommandAsync("-stack-list-frames"));
        }
    }


    private Task<GdbCommandResult> RunCommandAsync(string command, params string[] args)
    {
        return Task.Run(() => RunCommand(command, 10000, args));
    }

    private GdbCommandResult RunCommand(string command, int timeout = 10000, params string[] args)
    {
        lock (_gdbLock)
        {
            lock (_syncLock)
            {
                _lastResult = null;

                if (_sIn != null)
                {
                    if (!asyncMode)
                        lock (_eventLock)
                        {
                            if (_running)
                            {
                                OutputReceived?.Invoke(this,
                                    "Not possible to run commands while the target is running!");
                                return new GdbCommandResult("Not possible while the target is running")
                                    { Status = CommandStatus.Running };
                            }

                            _running = true;
                        }

                    try
                    {
                        var cmd = $" {command} {string.Join(" ", args)}";
                        CommandRun?.Invoke(this, cmd);

                        _sIn.WriteLine(cmd);

                        if (!Monitor.Wait(_syncLock, timeout))
                        {
                            _lastResult = new GdbCommandResult("") { Status = CommandStatus.Timeout };
                            //Tools.ThrowError("GDB Timeout", new TimeoutException("GDB timed out"));
                            OutputReceived?.Invoke(this, "^error, GDB timed out");
                        }

                        return _lastResult ?? _timeout;
                    }
                    catch (Exception e)
                    {
                        if (e is ObjectDisposedException)
                            return new GdbCommandResult("") { Status = CommandStatus.Error };

                        logger.LogError(e, e.Message);
                    }

                    return _lastResult ?? _timeout;
                }

                return _timeout;
            }
        }
    }

    private string FormatBreakPoint(BreakPoint bp)
    {
        return $"\"{bp.File.Replace('\\', '/')}:{bp.Line}\"";
    }

    public GdbCommandResult? Pause()
    {
        if (asyncMode) return RunCommand("-exec-interrupt");
        lock (_eventLock)
        {
            var c = 0;
            const int maxTries = 3;
            do
            {
                GdbHelper.SendCtrlC(_process!.Id);
                c++;
                if (c >= maxTries) break;
                if (!_running) return _lastResult;
            } while (!Monitor.Wait(_eventLock, 500));

            //Failed
            return new GdbCommandResult("") { Status = CommandStatus.Error };
        }
    }

    //Step
    public GdbCommandResult? Step()
    {
        return RunCommand("-exec-step");
    }

    //Step out
    public GdbCommandResult? Finish()
    {
        return RunCommand("-exec-finish");
    }

    //Step Over
    public GdbCommandResult? Next()
    {
        return RunCommand("-exec-next");
    }

    public GdbCommandResult? Continue()
    {
        return RunCommand("-exec-continue");
    }

    public GdbCommandResult? Run()
    {
        return RunCommand("-exec-run");
    }

    public GdbCommandResult? Print(string symbol)
    {
        return RunCommand("print " + symbol);
    }

    public GdbCommandResult? InsertBreakPoint(BreakPoint breakPoint)
    {
        return RunCommand("-break-insert " + FormatBreakPoint(breakPoint));
    }

    public GdbCommandResult? RemoveBreakPoint(BreakPoint breakPoint)
    {
        return RunCommand("clear " + FormatBreakPoint(breakPoint));
    }

    public GdbCommandResult? EvaluateExpression(string expression)
    {
        return RunCommand("-data-evaluate-expression " + expression);
    }
}