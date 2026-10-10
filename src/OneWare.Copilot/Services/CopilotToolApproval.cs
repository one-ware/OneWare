using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneWare.Copilot.Services;

/// <summary>
/// What "allow in this chat" approves: the approval keys that must all be approved, and the button text.
/// </summary>
internal sealed record ToolApprovalScope(IReadOnlyList<string> Keys, string Label);

/// <summary>
/// Decides how far an "allow in this chat" approval reaches. Terminal commands are approved per command
/// (e.g. "git status", "ls") instead of for the whole tool, like VS Code does; other tools per tool.
/// </summary>
internal static partial class CopilotToolApproval
{
    public const string TerminalToolName = "runTerminalCommand";

    private const int MaxPrefixesInLabel = 3;

    // Tools whose second word selects what they do ("git status" vs "git push").
    private static readonly HashSet<string> SubcommandTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "git", "npm", "pnpm", "yarn", "dotnet", "cargo", "docker", "podman", "kubectl", "pip", "pip3", "go",
        "gh", "apt", "apt-get", "brew", "conda", "make", "cmake", "west", "idf.py"
    };

    // Commands that run other commands; approving them would approve anything.
    private static readonly HashSet<string> WrapperCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "sudo", "su", "doas", "env", "xargs", "eval", "exec", "source", ".", "bash", "sh", "zsh", "fish", "dash",
        "ksh", "pwsh", "powershell", "cmd", "nohup", "time", "timeout", "watch", "nice", "ssh", "command",
        "builtin", "Invoke-Expression", "iex", "Start-Process", "start"
    };

    private static readonly string[] FindExecutingOptions = ["-exec", "-execdir", "-ok", "-okdir", "-delete"];

    /// <summary>
    /// The approval scope of a OneWare tool call, or null when no approval for the chat should be offered
    /// (e.g. a terminal command too complex to tell what it runs).
    /// </summary>
    public static ToolApprovalScope? ForTool(string toolName, string? friendlyName, JsonElement? arguments)
    {
        if (!string.Equals(toolName, TerminalToolName, StringComparison.Ordinal))
            return new ToolApprovalScope([$"tool:{toolName}"], $"Allow {friendlyName ?? toolName} in this chat");

        var command = TryGetString(arguments, "command");
        var prefixes = command == null ? null : GetCommandPrefixes(command);
        if (prefixes == null) return null;

        var shown = prefixes.Take(MaxPrefixesInLabel).Select(p => $"\"{p}\"");
        var label = $"Allow {string.Join(", ", shown)}{(prefixes.Count > MaxPrefixesInLabel ? ", …" : "")} in this chat";
        return new ToolApprovalScope(prefixes.Select(p => $"tool:{toolName}:{p}").ToList(), label);
    }

    /// <summary>
    /// The commands a terminal command line runs, as approval prefixes ("git status", "ls"), or null when it
    /// can't be told reliably (command substitution, subshells, multi-line scripts, commands that run others).
    /// </summary>
    public static IReadOnlyList<string>? GetCommandPrefixes(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        if (command.Contains("$(") || command.Contains('`') || command.Contains("<(") || command.Contains(">(") ||
            command.Contains('\n') || command.Contains('\r'))
            return null;

        var segments = SplitSegments(command);
        if (segments == null) return null;

        var prefixes = new List<string>();
        foreach (var segment in segments)
        {
            var tokens = Tokenize(segment);
            if (tokens == null) return null;

            // Leading environment assignments (FOO=bar cmd) don't change which command runs.
            var start = 0;
            while (start < tokens.Count && EnvAssignment().IsMatch(tokens[start])) start++;
            if (start == tokens.Count) continue;

            var identifier = tokens[start];
            var slash = identifier.LastIndexOfAny(['/', '\\']);
            if (slash >= 0) identifier = identifier[(slash + 1)..];

            if (!CommandName().IsMatch(identifier) || WrapperCommands.Contains(identifier)) return null;
            if (string.Equals(identifier, "find", StringComparison.OrdinalIgnoreCase) &&
                tokens.Any(t => FindExecutingOptions.Contains(t, StringComparer.Ordinal)))
                return null;

            var prefix = identifier;
            if (SubcommandTools.Contains(identifier) && start + 1 < tokens.Count &&
                Subcommand().IsMatch(tokens[start + 1]))
                prefix = $"{identifier} {tokens[start + 1]}";

            if (!prefixes.Contains(prefix, StringComparer.Ordinal)) prefixes.Add(prefix);
        }

        return prefixes.Count > 0 ? prefixes : null;
    }

    /// <summary>Splits at ";", "|", "||", "&amp;&amp;" and "&amp;" outside quotes; "2>&amp;1" and "&amp;>" stay intact.</summary>
    private static List<string>? SplitSegments(string command)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        char? quote = null;

        for (var i = 0; i < command.Length; i++)
        {
            var c = command[i];
            if (quote != null)
            {
                current.Append(c);
                if (c == quote) quote = null;
                else if (c == '\\' && quote == '"' && i + 1 < command.Length) current.Append(command[++i]);
                continue;
            }

            switch (c)
            {
                case '\'' or '"':
                    quote = c;
                    current.Append(c);
                    continue;
                case '\\' when i + 1 < command.Length:
                    current.Append(c).Append(command[++i]);
                    continue;
                case '&' when i > 0 && command[i - 1] == '>' || i + 1 < command.Length && command[i + 1] == '>':
                    current.Append(c);
                    continue;
                case ';' or '|' or '&':
                    segments.Add(current.ToString());
                    current.Clear();
                    continue;
                default:
                    current.Append(c);
                    continue;
            }
        }

        if (quote != null) return null;
        segments.Add(current.ToString());
        return segments;
    }

    /// <summary>Splits a segment into words, removing quotes; null for unbalanced quotes.</summary>
    private static List<string>? Tokenize(string segment)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inToken = false;
        char? quote = null;

        foreach (var c in segment)
        {
            if (quote != null)
            {
                if (c == quote) quote = null;
                else current.Append(c);
                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
                inToken = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (inToken) tokens.Add(current.ToString());
                current.Clear();
                inToken = false;
            }
            else
            {
                current.Append(c);
                inToken = true;
            }
        }

        if (quote != null) return null;
        if (inToken) tokens.Add(current.ToString());
        return tokens;
    }

    private static string? TryGetString(JsonElement? arguments, string name)
    {
        if (arguments is not { } element) return null;
        if (element.ValueKind == JsonValueKind.String)
        {
            var json = element.GetString();
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                element = JsonDocument.Parse(json).RootElement;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*=")]
    private static partial Regex EnvAssignment();

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.+-]*$")]
    private static partial Regex CommandName();

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex Subcommand();
}
