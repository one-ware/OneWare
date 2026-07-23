using System.Text;
using VtNetCore.Avalonia;

namespace OneWare.Terminal.Provider;

public class PseudoTerminalConnection(IPseudoTerminal terminal) : IConnection, IOutputFilter, IOutputSuppressor, IDisposable
{
    private CancellationTokenSource? _cancellationSource;
    private readonly OutputSequenceSuppressor _outputSuppressor = new();
    private long _outputVersion;

    public bool IsConnected { get; private set; }

    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    public event EventHandler<EventArgs>? Closed;

    public event EventHandler<TerminalCommandCompletedEventArgs>? CommandCompleted;

    public event EventHandler? UserInterrupted;

    public bool Connect()
    {
        _cancellationSource = new CancellationTokenSource();

        _ = ReadOutputAsync(_cancellationSource.Token);
        _ = ReadControlAsync(_cancellationSource.Token);

        IsConnected = true;

        terminal.Process.EnableRaisingEvents = true;
        terminal.Process.Exited += Process_Exited;

        return IsConnected;
    }

    public void Disconnect()
    {
        _cancellationSource?.Cancel();
        terminal.Dispose();
    }

    public void SendData(byte[] data)
    {
        _ = terminal.WriteAsync(data, 0, data.Length);
        if (data.Contains((byte)0x03) && _cancellationSource is { } cancellationSource)
            _ = NotifyUserInterruptedAsync(cancellationSource.Token);
    }

    public void KillProcess()
    {
        try
        {
            if (!terminal.Process.HasExited)
                terminal.Process.Kill(true);
        }
        catch
        {
            // Best effort: the process may already have exited or be unkillable.
        }
    }

    public void SuppressOutput(byte[] sequence)
    {
        _outputSuppressor.SuppressOutput(sequence);
    }

    public byte[] FilterOutput(byte[] data)
    {
        return _outputSuppressor.FilterOutput(data);
    }

    public void ResetOutputSuppression()
    {
        _outputSuppressor.Reset();
    }

    public void SetTerminalWindowSize(int columns, int rows)
    {
        terminal.SetSize(columns, rows);
    }

    public void Dispose()
    {
        _cancellationSource?.Cancel();

        Disconnect();
    }

    private void Process_Exited(object? sender, EventArgs e)
    {
        terminal.Process.Exited -= Process_Exited;
        _cancellationSource?.Cancel();

        Closed?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReadOutputAsync(CancellationToken cancellationToken)
    {
        var data = new byte[4096];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesReceived = await terminal.ReadAsync(data, 0, data.Length);
                if (bytesReceived <= 0) break;

                var receivedData = new byte[bytesReceived];
                Buffer.BlockCopy(data, 0, receivedData, 0, bytesReceived);
                DataReceived?.Invoke(this, new DataReceivedEventArgs { Data = receivedData });
                Interlocked.Increment(ref _outputVersion);
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ReadControlAsync(CancellationToken cancellationToken)
    {
        var data = new byte[256];
        var pending = new StringBuilder();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesReceived = await terminal.ReadControlAsync(data, 0, data.Length, cancellationToken);
                if (bytesReceived <= 0) break;

                pending.Append(Encoding.ASCII.GetString(data, 0, bytesReceived));
                while (TryReadControlFrame(pending, out var executionId, out var exitCode))
                {
                    await WaitForOutputDrainAsync(cancellationToken);
                    CommandCompleted?.Invoke(this, new TerminalCommandCompletedEventArgs(executionId, exitCode));

                    var acknowledgement = Encoding.ASCII.GetBytes($"{executionId}\n");
                    await terminal.WriteControlAsync(acknowledgement, 0, acknowledgement.Length, cancellationToken);
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task WaitForOutputDrainAsync(CancellationToken cancellationToken)
    {
        var previousVersion = Volatile.Read(ref _outputVersion);
        var stableSamples = 0;

        while (stableSamples < 3)
        {
            await Task.Delay(20, cancellationToken);
            var currentVersion = Volatile.Read(ref _outputVersion);
            if (currentVersion == previousVersion)
            {
                stableSamples++;
            }
            else
            {
                previousVersion = currentVersion;
                stableSamples = 0;
            }
        }
    }

    private async Task NotifyUserInterruptedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WaitForOutputDrainAsync(cancellationToken);
            UserInterrupted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
        }
    }

    internal static bool TryReadControlFrame(StringBuilder pending, out string executionId, out int exitCode)
    {
        executionId = string.Empty;
        exitCode = 0;

        var newlineIndex = pending.ToString().IndexOf('\n');
        if (newlineIndex < 0) return false;

        var line = pending.ToString(0, newlineIndex).TrimEnd('\r');
        pending.Remove(0, newlineIndex + 1);

        var separatorIndex = line.LastIndexOf(':');
        if (separatorIndex <= 0 || !int.TryParse(line[(separatorIndex + 1)..], out exitCode))
            return false;

        executionId = line[..separatorIndex];
        return true;
    }
}

public sealed class TerminalCommandCompletedEventArgs(string executionId, int exitCode) : EventArgs
{
    public string ExecutionId { get; } = executionId;
    public int ExitCode { get; } = exitCode;
}
