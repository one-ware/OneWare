using System.IO.Pipes;
using System.Text;

namespace OneWare.Essentials.Helpers;

/// <summary>
/// Per-user single-instance lock and IPC channel of the running OneWare Studio.
/// Used by the desktop app (lock + server) and by CLI modules or a second instance to talk to it.
/// </summary>
public sealed class StudioInstanceChannel
{
    /// <summary>
    /// Prefix that marks an IPC command. Messages without it are treated as a path or oneware:// URL to open.
    /// </summary>
    public const string CommandPrefix = "cmd:";

    /// <summary>
    /// Brings the main window to the front.
    /// </summary>
    public const string ActivateCommand = "activate";

    /// <summary>
    /// Requests a regular shutdown (unsaved changes may still be confirmed by the user).
    /// </summary>
    public const string ShutdownCommand = "shutdown";

    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(2);

    public StudioInstanceChannel(string lockFilePath, string pipeName)
    {
        LockFilePath = lockFilePath;
        PipeName = pipeName;
    }

    /// <summary>
    /// Channel used by OneWare Studio for the current user.
    /// </summary>
    public static StudioInstanceChannel Default { get; } = CreateDefault();

    public string LockFilePath { get; }

    /// <summary>
    /// Pipe name, or the absolute socket path on Unix.
    /// </summary>
    public string PipeName { get; }

    public static string FormatCommand(string name, string? argument = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\n'))
            throw new ArgumentException("Invalid command name.", nameof(name));

        return argument is null ? $"{CommandPrefix}{name}" : $"{CommandPrefix}{name}\n{argument}";
    }

    public static bool TryParseCommand(string message, out string name, out string? argument)
    {
        name = string.Empty;
        argument = null;

        if (!message.StartsWith(CommandPrefix, StringComparison.Ordinal))
            return false;

        var body = message[CommandPrefix.Length..];
        var separator = body.IndexOf('\n');
        name = (separator < 0 ? body : body[..separator]).Trim();
        argument = separator < 0 ? null : body[(separator + 1)..];
        return name.Length > 0;
    }

    /// <summary>
    /// Tries to become the primary instance. Returns the held lock, or null if another instance owns it.
    /// Keep the returned stream open for the lifetime of the app.
    /// </summary>
    public FileStream? TryAcquirePrimaryLock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LockFilePath)!);

        // A short retry covers the window in which another process is only probing the lock.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var stream = new FileStream(LockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                    FileShare.None, 1, FileOptions.DeleteOnClose);
                var pid = Encoding.UTF8.GetBytes(Environment.ProcessId.ToString());
                stream.SetLength(0);
                stream.Write(pid);
                stream.Flush();
                return stream;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }

        return null;
    }

    /// <summary>
    /// True when another process currently holds the primary instance lock.
    /// </summary>
    public bool IsStudioRunning()
    {
        if (!File.Exists(LockFilePath))
            return false;

        try
        {
            using var probe = new FileStream(LockFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // Windows reports a lock file that is pending deletion (instance is exiting) as access denied.
            return true;
        }
    }

    /// <summary>
    /// Waits until no process holds the primary instance lock.
    /// </summary>
    public async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (IsStudioRunning())
        {
            if (DateTime.UtcNow >= deadline)
                return false;

            await Task.Delay(100, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Sends a command to the running instance. Returns false when no instance could be reached.
    /// </summary>
    public Task<bool> SendCommandAsync(string name, string? argument = null,
        CancellationToken cancellationToken = default)
    {
        return SendMessageAsync(FormatCommand(name, argument), cancellationToken);
    }

    /// <summary>
    /// Sends a raw message (command, path or oneware:// URL). Returns false when no instance could be reached.
    /// </summary>
    public async Task<bool> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectTimeout.CancelAfter(DefaultConnectTimeout);
            await client.ConnectAsync(connectTimeout.Token);

            await using var writer = new StreamWriter(client, new UTF8Encoding(false));
            await writer.WriteAsync(message.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Receives messages until cancelled. <paramref name="onMessage" /> is called on a background thread.
    /// </summary>
    public async Task RunServerAsync(Action<string> onMessage, Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        NamedPipeServerStream? server = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
                try
                {
                    server ??= CreateServer();
                    await server.WaitForConnectionAsync(cancellationToken);

                    // Create the next instance before releasing this one. On Unix the listening socket is closed
                    // when the last instance is disposed, which would drop clients that already connected.
                    await using var connected = server;
                    server = null;
                    server = CreateServer();

                    using var reader = new StreamReader(connected, Encoding.UTF8);
                    var message = await reader.ReadToEndAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(message))
                        onMessage(message);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    onError?.Invoke(ex);
                    if (server != null)
                    {
                        await server.DisposeAsync();
                        server = null;
                    }

                    await Task.Delay(1000, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
                }
        }
        finally
        {
            if (server != null)
                await server.DisposeAsync();
        }
    }

    private NamedPipeServerStream CreateServer()
    {
        return new NamedPipeServerStream(PipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    private static StudioInstanceChannel CreateDefault()
    {
        var directory = GetUserRuntimeDirectory();
        var pipeName = OperatingSystem.IsWindows()
            ? $"oneware-studio-ipc-{SanitizeName(Environment.UserName)}"
            : Path.Combine(directory, "ipc.sock");

        return new StudioInstanceChannel(Path.Combine(directory, "oneware-studio.lock"), pipeName);
    }

    private static string GetUserRuntimeDirectory()
    {
        // %TEMP% (Windows) and $TMPDIR (macOS) are already per user; /tmp on Linux is shared.
        if (!OperatingSystem.IsLinux())
            return Path.Combine(Path.GetTempPath(), "OneWare");

        var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var directory = !string.IsNullOrWhiteSpace(runtimeDirectory) && Directory.Exists(runtimeDirectory)
            ? Path.Combine(runtimeDirectory, "oneware")
            : Path.Combine(Path.GetTempPath(), $"oneware-{SanitizeName(Environment.UserName)}");

        try
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception)
        {
            // Creation is retried (and reported) when the lock is acquired.
        }

        return directory;
    }

    private static string SanitizeName(string value)
    {
        var sanitized = new string(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        return string.IsNullOrEmpty(sanitized) ? "user" : sanitized;
    }
}
