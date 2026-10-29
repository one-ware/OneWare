using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GitHub.Copilot;

namespace OneWare.Copilot.Connectors;

public enum McpConnectorKind
{
    Local,
    Remote
}

public sealed record McpConnectorValue(string Key, string Value);

/// <summary>
/// An MCP server ("connector") in the Copilot CLI's user configuration (<c>mcp-config.json</c>). Values may reference
/// <c>${VAR}</c> environment variables, which the CLI resolves from its process environment. Variables stored in
/// OneWare (secrets) are filled in by OneWare, only for the connector that references them.
/// </summary>
public sealed partial class McpConnector
{
    // Fields edited by OneWare; everything else from the CLI configuration (tools, timeout, …) is kept as is.
    private static readonly string[] EditedFields =
        ["type", "command", "args", "cwd", "env", "url", "headers", "oauthClientId", "oauthPublicClient"];

    public string Name { get; set; } = string.Empty;

    public McpConnectorKind Kind { get; set; }

    public bool Enabled { get; set; } = true;

    public string Command { get; set; } = string.Empty;

    public List<string> Args { get; set; } = [];

    public string? WorkingDirectory { get; set; }

    public List<McpConnectorValue> Env { get; set; } = [];

    public string Url { get; set; } = string.Empty;

    /// <summary>Uses the legacy SSE transport instead of streamable HTTP.</summary>
    public bool UseSse { get; set; }

    public List<McpConnectorValue> Headers { get; set; } = [];

    public string? OAuthClientId { get; set; }

    public bool? OAuthPublicClient { get; set; }

    public JsonObject OtherFields { get; set; } = new();

    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex VariableRegex();

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex NameRegex();

    public static bool IsValidName(string name) => NameRegex().IsMatch(name) && GetVariableKey(name).Length > 0;

    public static McpConnector FromConfig(string name, JsonObject config, bool enabled)
    {
        var url = GetString(config, "url");
        var command = GetString(config, "command");
        var type = GetString(config, "type")?.Trim().ToLowerInvariant();
        var connector = new McpConnector
        {
            Name = name,
            Enabled = enabled,
            Kind = type is "http" or "sse" || (type == null && command == null && url != null)
                ? McpConnectorKind.Remote
                : McpConnectorKind.Local,
            UseSse = type == "sse",
            Command = command ?? string.Empty,
            Args = GetStrings(config, "args"),
            WorkingDirectory = GetString(config, "cwd"),
            Env = GetValues(config, "env"),
            Url = url ?? string.Empty,
            Headers = GetValues(config, "headers"),
            OAuthClientId = GetString(config, "oauthClientId"),
            OAuthPublicClient = config["oauthPublicClient"] is JsonValue publicClient &&
                                publicClient.TryGetValue<bool>(out var isPublic)
                ? isPublic
                : null
        };

        foreach (var (key, value) in config)
            if (!EditedFields.Contains(key))
                connector.OtherFields[key] = value?.DeepClone();

        return connector;
    }

    /// <summary>The configuration written to the CLI. Values keep their <c>${VAR}</c> references.</summary>
    public JsonObject ToConfig()
    {
        var config = new JsonObject();

        if (Kind == McpConnectorKind.Local)
        {
            config["type"] = "stdio";
            config["command"] = Command.Trim();
            if (Args.Count > 0) config["args"] = new JsonArray(Args.Select(x => (JsonNode?)x).ToArray());
            if (!string.IsNullOrWhiteSpace(WorkingDirectory)) config["cwd"] = WorkingDirectory.Trim();
            if (Env.Count > 0) config["env"] = ToObject(Env);
        }
        else
        {
            config["type"] = UseSse ? "sse" : "http";
            config["url"] = Url.Trim();
            if (Headers.Count > 0) config["headers"] = ToObject(Headers);
            if (!string.IsNullOrWhiteSpace(OAuthClientId)) config["oauthClientId"] = OAuthClientId.Trim();
            if (OAuthPublicClient is { } isPublic) config["oauthPublicClient"] = isPublic;
        }

        foreach (var (key, value) in OtherFields) config[key] = value?.DeepClone();
        config["tools"] ??= new JsonArray("*");

        return config;
    }

    /// <summary>
    /// The connector as a session's own MCP server, which replaces the one in the configuration. <c>null</c> for SSE
    /// servers, which the SDK's session configuration can't express.
    /// </summary>
    public McpServerConfig? ToServerConfig()
    {
        if (Kind == McpConnectorKind.Remote && UseSse) return null;

        var config = ToConfig();
        McpServerConfig result = Kind == McpConnectorKind.Local
            ? new McpStdioServerConfig
            {
                Command = Command.Trim(),
                Args = Args.ToList(),
                Env = Env.Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .ToDictionary(x => x.Key.Trim(), x => x.Value),
                WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory.Trim()
            }
            : new McpHttpServerConfig
            {
                Url = Url.Trim(),
                Headers = Headers.Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .ToDictionary(x => x.Key.Trim(), x => x.Value),
                OauthClientId = string.IsNullOrWhiteSpace(OAuthClientId) ? null : OAuthClientId.Trim(),
                OauthPublicClient = OAuthPublicClient
            };

        result.Tools = GetStrings(config, "tools");
        if (config["timeout"] is JsonValue timeout && timeout.TryGetValue<int>(out var ms)) result.Timeout = ms;
        return result;
    }

    /// <summary>Environment variables the connector references with <c>${VAR}</c>, sorted.</summary>
    public IReadOnlyList<string> GetVariables()
    {
        IEnumerable<string> values = Kind == McpConnectorKind.Local
            ? [Command, WorkingDirectory ?? string.Empty, ..Args, ..Env.Select(x => x.Value)]
            : [..Headers.Select(x => x.Value)];

        return values.SelectMany(x => VariableRegex().Matches(x).Select(m => m.Groups["name"].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Variable a secret value of this connector is stored under, e.g. <c>ONEWARE_MCP_GITHUB__AUTHORIZATION</c>.
    /// The parts never contain <c>__</c>, so different connectors can't produce the same name as long as their
    /// <see cref="GetVariableKey" /> differs.
    /// </summary>
    public static string GetSecretVariableName(string connector, string key) =>
        $"{GetSecretVariablePrefix(connector)}{GetVariableKey(key)}";

    /// <summary>Prefix of all variables <see cref="GetSecretVariableName" /> generates for a connector.</summary>
    public static string GetSecretVariablePrefix(string connector) => $"ONEWARE_MCP_{GetVariableKey(connector)}__";

    /// <summary>
    /// The form of a connector name or value key used in generated variable names. Names or keys with the same form
    /// (e.g. <c>my-server</c> and <c>My_Server</c>) would share variables, so they must not be used side by side.
    /// </summary>
    public static string GetVariableKey(string value) =>
        string.Join('_', new string(value.ToUpperInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')
            .ToArray()).Split('_', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// A copy with the given variables filled in. Unknown <c>${VAR}</c> references stay as they are.
    /// </summary>
    public McpConnector WithVariables(IReadOnlyDictionary<string, string> values)
    {
        var result = Clone();
        string Fill(string value) => VariableRegex().Replace(value,
            m => values.TryGetValue(m.Groups["name"].Value, out var v) ? v : m.Value);

        result.Command = Fill(result.Command);
        if (result.WorkingDirectory != null) result.WorkingDirectory = Fill(result.WorkingDirectory);
        result.Args = result.Args.Select(Fill).ToList();
        result.Env = result.Env.Select(x => x with { Value = Fill(x.Value) }).ToList();
        result.Headers = result.Headers.Select(x => x with { Value = Fill(x.Value) }).ToList();
        return result;
    }

    /// <summary>Whether <paramref name="value" /> is exactly one <c>${VAR}</c> reference.</summary>
    public static bool IsVariableReference(string value, out string variable)
    {
        var match = VariableRegex().Match(value);
        variable = match.Success && match.Length == value.Length ? match.Groups["name"].Value : string.Empty;
        return variable.Length > 0;
    }

    public static string ToVariableReference(string variable) => "${" + variable + "}";

    public string Describe() => Kind == McpConnectorKind.Remote
        ? Url
        : string.Join(' ', new[] { Command }.Concat(Args).Where(x => !string.IsNullOrEmpty(x)));

    public McpConnector Clone()
    {
        var clone = FromConfig(Name, ToConfig(), Enabled);
        // ToConfig adds the default tool filter; a clone must not differ from the original.
        if (!OtherFields.ContainsKey("tools")) clone.OtherFields.Remove("tools");
        return clone;
    }

    private static JsonObject ToObject(IEnumerable<McpConnectorValue> values)
    {
        var result = new JsonObject();
        foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
            result[value.Key.Trim()] = value.Value;
        return result;
    }

    private static string? GetString(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static List<string> GetStrings(JsonObject obj, string key) =>
        obj[key] is JsonArray array
            ? array.OfType<JsonValue>().Select(x => x.TryGetValue<string>(out var s) ? s : null).OfType<string>()
                .ToList()
            : [];

    private static List<McpConnectorValue> GetValues(JsonObject obj, string key) =>
        obj[key] is JsonObject map
            ? map.Where(x => x.Value is JsonValue v && v.TryGetValue<string>(out _))
                .Select(x => new McpConnectorValue(x.Key, x.Value!.GetValue<string>())).ToList()
            : [];
}
