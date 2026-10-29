using System.Diagnostics;
using System.IO;

namespace OneWare.Copilot.Services;

/// <summary>
/// The environment of the user's login shell. Apps started from a desktop launcher don't see variables exported
/// in shell profiles (tokens referenced by MCP configurations, for example), so they are read from there like
/// VS Code does. Read once per process, never on Windows.
/// </summary>
internal static class LoginShellEnvironment
{
    private const string Marker = "__ONEWARE_SHELL_ENV__";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static Task<IReadOnlyDictionary<string, string>>? _environment;

    public static Task<IReadOnlyDictionary<string, string>> GetAsync() =>
        LazyInitializer.EnsureInitialized(ref _environment, () => Task.Run(ReadAsync));

    private static async Task<IReadOnlyDictionary<string, string>> ReadAsync()
    {
        var empty = new Dictionary<string, string>();
        if (OperatingSystem.IsWindows() || OperatingSystem.IsBrowser()) return empty;

        var shell = ResolveShell(Environment.GetEnvironmentVariable("SHELL"),
            Environment.GetEnvironmentVariable("PATH"), ReadAccountShell);

        try
        {
            var start = new ProcessStartInfo(shell)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            // Interactive and login, so both ~/.profile and ~/.bashrc / ~/.zshrc style files are applied.
            foreach (var arg in new[] { "-i", "-l", "-c", $"printf '{Marker}'; env -0" })
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start);
            if (process == null) return empty;
            process.StandardInput.Close();

            using var cts = new CancellationTokenSource(Timeout);
            var output = process.StandardOutput.ReadToEndAsync(cts.Token);
            _ = process.StandardError.ReadToEndAsync(cts.Token);
            try
            {
                await process.WaitForExitAsync(cts.Token);
                return Parse(await output);
            }
            catch (OperationCanceledException)
            {
                process.Kill(true);
                return empty;
            }
        }
        catch (Exception)
        {
            return empty;
        }
    }

    /// <summary>
    /// The user's shell. <c>SHELL</c> may be a bare name (some launchers and IDE run configurations set
    /// <c>SHELL=bash</c>) or missing; falling back to <c>/bin/sh</c> right away would skip <c>~/.bashrc</c> /
    /// <c>~/.zshrc</c>, where tools such as nvm are set up.
    /// </summary>
    internal static string ResolveShell(string? shell, string? path, Func<string?> accountShell)
    {
        if (FindExecutable(shell, path) is { } fromVariable) return fromVariable;
        if (FindExecutable(accountShell(), path) is { } fromAccount) return fromAccount;
        return "/bin/sh";
    }

    private static string? FindExecutable(string? name, string? path)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();
        if (Path.IsPathRooted(name)) return File.Exists(name) ? name : null;
        if (name.Contains('/')) return null;

        return (path ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(["/usr/bin", "/bin"])
            .Select(x => Path.Combine(x, name))
            .FirstOrDefault(File.Exists);
    }

    // The login shell of the account, from /etc/passwd.
    private static string? ReadAccountShell()
    {
        try
        {
            var user = Environment.UserName;
            return File.ReadLines("/etc/passwd")
                .Select(x => x.Split(':'))
                .FirstOrDefault(x => x.Length >= 7 && x[0] == user)?[6];
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static IReadOnlyDictionary<string, string> Parse(string output)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        // Profiles may print banners before the marker.
        var start = output.LastIndexOf(Marker, StringComparison.Ordinal);
        if (start < 0) return result;

        foreach (var entry in output[(start + Marker.Length)..].Split('\0'))
        {
            var separator = entry.IndexOf('=');
            if (separator <= 0) continue;
            result[entry[..separator]] = entry[(separator + 1)..];
        }

        return result;
    }
}
