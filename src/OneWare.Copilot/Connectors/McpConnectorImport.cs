using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OneWare.Copilot.Connectors;

/// <param name="OAuthClientSecret">Secret of a pre-registered OAuth client (<c>oauthClientSecret</c>), if declared.</param>
public sealed record ImportedMcpConnector(McpConnector Connector, string? OAuthClientSecret);

/// <summary>
/// Reads connectors from MCP configuration files in the formats of the Copilot CLI (<c>mcpServers</c>), VS Code
/// (<c>servers</c>) and Claude Code (top-level map).
/// </summary>
public static partial class McpConnectorImport
{
    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_:]*)(?::-(?<default>[^}]*))?\}")]
    private static partial Regex VariableRegex();

    public static IReadOnlyList<ImportedMcpConnector> Read(string file, Action<string> warn)
    {
        JsonNode? document;
        try
        {
            document = JsonNode.Parse(File.ReadAllText(file), documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            warn($"Failed to read {file}: {e.Message}");
            return [];
        }

        if (document is not JsonObject root)
        {
            warn($"{file} must contain a JSON object.");
            return [];
        }

        var map = root["mcpServers"] as JsonObject ?? root["servers"] as JsonObject ?? root;
        var directory = Path.GetDirectoryName(Path.GetFullPath(file))!;
        var result = new List<ImportedMcpConnector>();

        foreach (var (name, node) in map)
        {
            if (node is not JsonObject declaration) continue;
            if (!McpConnector.IsValidName(name))
            {
                warn($"'{name}' is not a valid connector name (letters, digits, '.', '-' and '_').");
                continue;
            }

            if (Convert(name, declaration, directory, message => warn($"{name}: {message}")) is { } imported)
                result.Add(imported);
        }

        return result;
    }

    private static ImportedMcpConnector? Convert(string name, JsonObject declaration, string directory,
        Action<string> warn)
    {
        string Normalize(string value) => NormalizeVariables(value, directory, warn);

        var command = GetString(declaration, "command");
        var url = GetString(declaration, "url");
        var type = GetString(declaration, "type")?.Trim().ToLowerInvariant() switch
        {
            null or "" => command != null ? "stdio" : url != null ? "http" : null,
            "stdio" or "local" => "stdio",
            "http" or "streamable-http" or "streamablehttp" => "http",
            "sse" => "sse",
            var other => Unsupported(other)
        };

        string? Unsupported(string value)
        {
            warn($"the type '{value}' is not supported.");
            return null;
        }

        var config = new JsonObject();
        string? clientSecret = null;

        if (type == "stdio" && !string.IsNullOrWhiteSpace(command))
        {
            config["command"] = Normalize(command);
            if (declaration["args"] is JsonArray args)
                config["args"] = new JsonArray(args.OfType<JsonValue>()
                    .Select(x => x.TryGetValue<string>(out var s) ? (JsonNode?)Normalize(s) : null)
                    .OfType<JsonNode>().ToArray());
            if (declaration["env"] is JsonObject env) config["env"] = MapValues(env, Normalize);
            // Project files expect to run in their folder, a connector runs wherever the chat is.
            var cwd = GetString(declaration, "cwd");
            config["cwd"] = string.IsNullOrWhiteSpace(cwd) ? directory : Path.GetFullPath(Normalize(cwd), directory);
        }
        else if (type is "http" or "sse" && !string.IsNullOrWhiteSpace(url))
        {
            config["type"] = type;
            // The CLI only accepts a literal url.
            config["url"] = ExpandVariables(url, directory, warn);
            if (declaration["headers"] is JsonObject headers) config["headers"] = MapValues(headers, Normalize);
            if (GetString(declaration, "oauthClientId") is { } clientId)
                config["oauthClientId"] = ExpandVariables(clientId, directory, warn);
            if (declaration["oauthPublicClient"]?.DeepClone() is { } publicClient)
                config["oauthPublicClient"] = publicClient;
            if (GetString(declaration, "oauthClientSecret") is { } secret)
                clientSecret = ExpandVariables(secret, directory, warn);
        }
        else
        {
            if (type != null || command != null || url != null)
                warn(type == "stdio" ? "a command is required." : "a url is required.");
            else warn("a command or a url is required.");
            return null;
        }

        foreach (var key in new[] { "tools", "timeout" })
            if (declaration[key]?.DeepClone() is { } value)
                config[key] = value;

        var enabled = !(declaration["disabled"] is JsonValue disabled && disabled.TryGetValue<bool>(out var off) &&
                        off);
        return new ImportedMcpConnector(McpConnector.FromConfig(name, config, enabled), clientSecret);
    }

    /// <summary>
    /// Rewrites references to the CLI's <c>${VAR}</c> syntax: <c>${env:VAR}</c> becomes <c>${VAR}</c> and
    /// <c>${workspaceFolder}</c> the folder of the imported file.
    /// </summary>
    internal static string NormalizeVariables(string value, string workspaceFolder, Action<string> warn) =>
        VariableRegex().Replace(value, match =>
        {
            var name = match.Groups["name"].Value;
            if (name is "workspaceFolder" or "workspaceRoot") return workspaceFolder;
            if (name.StartsWith("env:", StringComparison.Ordinal)) name = name[4..];
            if (name.Contains(':')) return match.Value;
            if (match.Groups["default"].Success)
                warn($"the default value of ${{{name}}} is not supported and was dropped.");
            return McpConnector.ToVariableReference(name);
        });

    private static string ExpandVariables(string value, string workspaceFolder, Action<string> warn) =>
        VariableRegex().Replace(value, match =>
        {
            var name = match.Groups["name"].Value;
            if (name is "workspaceFolder" or "workspaceRoot") return workspaceFolder;
            if (name.StartsWith("env:", StringComparison.Ordinal)) name = name[4..];

            var resolved = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(resolved)) return resolved;
            if (match.Groups["default"].Success) return match.Groups["default"].Value;

            warn($"${{{name}}} is not defined.");
            return string.Empty;
        });

    private static JsonObject MapValues(JsonObject values, Func<string, string> map)
    {
        var result = new JsonObject();
        foreach (var (key, value) in values)
            if (value is JsonValue v && v.TryGetValue<string>(out var text))
                result[key] = map(text);
        return result;
    }

    private static string? GetString(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
