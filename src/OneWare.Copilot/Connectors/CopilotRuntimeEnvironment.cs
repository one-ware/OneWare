using System.Collections;
using System.IO;

namespace OneWare.Copilot.Connectors;

/// <summary>Environment of the Copilot CLI process, which MCP servers inherit and <c>${VAR}</c> references use.</summary>
internal static class CopilotRuntimeEnvironment
{
    /// <summary>
    /// Merges the login shell's environment (variables and tools such as nvm's <c>npx</c> that apps started from a
    /// desktop launcher don't see) with OneWare's own, which wins. <c>PATH</c> keeps the login shell's entries
    /// first and appends OneWare's missing ones. Every MCP server inherits this, so it must not contain secrets
    /// stored in OneWare.
    /// </summary>
    public static Dictionary<string, string> Merge(IReadOnlyDictionary<string, string> process,
        IReadOnlyDictionary<string, string> loginShell, bool ignoreCase)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var result = new Dictionary<string, string>(loginShell, comparer);
        foreach (var (key, value) in process) result[key] = value;

        if (loginShell.TryGetValue("PATH", out var loginPath) && !string.IsNullOrEmpty(loginPath))
        {
            var entries = loginPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (process.TryGetValue("PATH", out var processPath))
                entries.AddRange(processPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => !entries.Contains(x, comparer)));
            result["PATH"] = string.Join(Path.PathSeparator, entries.Distinct(comparer));
        }

        return result;
    }

    public static Dictionary<string, string> GetProcessEnvironment()
    {
        var result = new Dictionary<string, string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            if (entry is { Key: string key, Value: string value })
                result[key] = value;
        return result;
    }
}
