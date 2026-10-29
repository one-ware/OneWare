using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GitHub.Copilot;

namespace OneWare.Copilot.Connectors;

public enum McpConnectorKind
{
    Local,
    Remote
}

/// <summary>An environment variable or header. Secret values are kept in the OS credential store.</summary>
public sealed record McpConnectorValue(string Key, string Value, bool IsSecret = false);

/// <summary>
/// An IDE-wide MCP server ("connector") from OneWare's <c>mcp.json</c>. The file uses the Copilot CLI's server format
/// plus a few OneWare fields (<see cref="IdField" />, <see cref="DisabledField" /> and the names of secret values);
/// the secret values themselves are kept in the OS credential store under the connector's <see cref="Id" />.
/// <c>${VAR}</c> references are expanded by the runtime from its environment.
/// </summary>
public sealed partial class McpConnector
{
    internal const string IdField = "id";
    internal const string DisabledField = "disabled";
    internal const string SecretEnvField = "secretEnv";
    internal const string SecretHeadersField = "secretHeaders";

    // Fields edited by OneWare; everything else (tools, timeout, …) is kept as is.
    private static readonly string[] EditedFields =
    [
        IdField, DisabledField, SecretEnvField, SecretHeadersField,
        "type", "command", "args", "cwd", "env", "url", "headers", "oauthClientId", "oauthPublicClient"
    ];

    private static readonly string[] SecretKeyMarkers =
        ["TOKEN", "SECRET", "PASSWORD", "PASSWD", "API_KEY", "APIKEY", "API-KEY", "ACCESS_KEY"];

    /// <summary>Stable identity, independent of the name; secrets are stored under it.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

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

    public static bool IsValidName(string name) => NameRegex().IsMatch(name) && name.Any(char.IsAsciiLetterOrDigit);

    /// <summary>
    /// Reads a server in the Copilot CLI format, with OneWare's fields if present. Values of keys listed as secret
    /// are taken from the configuration if it has them (e.g. when importing), otherwise they are empty.
    /// </summary>
    public static McpConnector FromConfig(string name, JsonObject config)
    {
        var url = GetString(config, "url");
        var command = GetString(config, "command");
        var type = GetString(config, "type")?.Trim().ToLowerInvariant();
        var connector = new McpConnector
        {
            Name = name,
            Enabled = !(config[DisabledField] is JsonValue disabled && disabled.TryGetValue<bool>(out var off) && off),
            Kind = type is "http" or "sse" || (type == null && command == null && url != null)
                ? McpConnectorKind.Remote
                : McpConnectorKind.Local,
            UseSse = type == "sse",
            Command = command ?? string.Empty,
            Args = GetStrings(config, "args"),
            WorkingDirectory = GetString(config, "cwd"),
            Env = GetValues(config, "env", GetStrings(config, SecretEnvField)),
            Url = url ?? string.Empty,
            Headers = GetValues(config, "headers", GetStrings(config, SecretHeadersField)),
            OAuthClientId = GetString(config, "oauthClientId"),
            OAuthPublicClient = config["oauthPublicClient"] is JsonValue publicClient &&
                                publicClient.TryGetValue<bool>(out var isPublic)
                ? isPublic
                : null
        };

        if (GetString(config, IdField) is { Length: > 0 } id) connector.Id = id;

        foreach (var (key, value) in config)
            if (!EditedFields.Contains(key))
                connector.OtherFields[key] = value?.DeepClone();

        return connector;
    }

    /// <summary>The entry written to OneWare's <c>mcp.json</c>: without secret values, with OneWare's fields.</summary>
    public JsonObject ToStoredConfig()
    {
        var config = new JsonObject { [IdField] = Id };
        foreach (var (key, value) in ToCliConfig(includeSecrets: false)) config[key] = value?.DeepClone();

        var values = Kind == McpConnectorKind.Local ? Env : Headers;
        var secretKeys = values.Where(x => x.IsSecret && !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => (JsonNode?)x.Key.Trim()).ToArray();
        if (secretKeys.Length > 0)
            config[Kind == McpConnectorKind.Local ? SecretEnvField : SecretHeadersField] = new JsonArray(secretKeys);
        if (!Enabled) config[DisabledField] = true;
        return config;
    }

    /// <summary>
    /// The configuration in the Copilot CLI format, as the runtime starts it. Values keep their <c>${VAR}</c>
    /// references. Without <paramref name="includeSecrets" />, secret values are left out.
    /// </summary>
    public JsonObject ToCliConfig(bool includeSecrets = true)
    {
        var config = new JsonObject();

        if (Kind == McpConnectorKind.Local)
        {
            config["type"] = "stdio";
            config["command"] = Command.Trim();
            if (Args.Count > 0) config["args"] = new JsonArray(Args.Select(x => (JsonNode?)x).ToArray());
            if (!string.IsNullOrWhiteSpace(WorkingDirectory)) config["cwd"] = WorkingDirectory.Trim();
            if (ToObject(Env, includeSecrets) is { Count: > 0 } env) config["env"] = env;
        }
        else
        {
            config["type"] = UseSse ? "sse" : "http";
            config["url"] = Url.Trim();
            if (ToObject(Headers, includeSecrets) is { Count: > 0 } headers) config["headers"] = headers;
            if (!string.IsNullOrWhiteSpace(OAuthClientId)) config["oauthClientId"] = OAuthClientId.Trim();
            if (OAuthPublicClient is { } isPublic) config["oauthPublicClient"] = isPublic;
        }

        foreach (var (key, value) in OtherFields) config[key] = value?.DeepClone();
        if (includeSecrets) config["tools"] ??= new JsonArray("*");

        return config;
    }

    /// <summary>
    /// The connector as a session MCP server, with secret values included. <see langword="null" /> for SSE servers,
    /// which the SDK's session configuration can't express; they are started with <see cref="ToCliConfig" />.
    /// </summary>
    public McpServerConfig? ToServerConfig()
    {
        if (Kind == McpConnectorKind.Remote && UseSse) return null;

        var config = ToCliConfig();
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

    /// <summary>The secret values currently in the connector.</summary>
    public McpConnectorSecrets GetSecrets(string? oauthClientSecret) => new()
    {
        Env = Env.Where(x => x.IsSecret && !string.IsNullOrWhiteSpace(x.Key) && x.Value.Length > 0)
            .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.Ordinal),
        Headers = Headers.Where(x => x.IsSecret && !string.IsNullOrWhiteSpace(x.Key) && x.Value.Length > 0)
            .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.Ordinal),
        OAuthClientSecret = string.IsNullOrEmpty(oauthClientSecret) ? null : oauthClientSecret
    };

    /// <summary>A copy with the stored secret values filled in.</summary>
    public McpConnector WithSecrets(McpConnectorSecrets secrets)
    {
        var result = Clone();
        result.Env = Fill(result.Env, secrets.Env);
        result.Headers = Fill(result.Headers, secrets.Headers);
        return result;

        static List<McpConnectorValue> Fill(List<McpConnectorValue> values, IReadOnlyDictionary<string, string> stored) =>
            values.Select(x => x.IsSecret && stored.TryGetValue(x.Key.Trim(), out var value) ? x with { Value = value } : x)
                .ToList();
    }

    /// <summary>Environment variables the connector references with <c>${VAR}</c>, sorted.</summary>
    public IReadOnlyList<string> GetVariables()
    {
        IEnumerable<string> values = Kind == McpConnectorKind.Local
            ? [Command, WorkingDirectory ?? string.Empty, ..Args, ..Env.Where(x => !x.IsSecret).Select(x => x.Value)]
            : [..Headers.Where(x => !x.IsSecret).Select(x => x.Value)];

        return values.SelectMany(x => VariableRegex().Matches(x).Select(m => m.Groups["name"].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public static string ToVariableReference(string variable) => "${" + variable + "}";

    public string Describe() => Kind == McpConnectorKind.Remote
        ? Url
        : string.Join(' ', new[] { Command }.Concat(Args).Where(x => !string.IsNullOrEmpty(x)));

    public McpConnector Clone()
    {
        var clone = FromConfig(Name, ToStoredConfig());
        // Stored configurations leave secret values out; a clone keeps them.
        clone.Env = Env.ToList();
        clone.Headers = Headers.ToList();
        return clone;
    }

    /// <summary>Whether a value with this key is most likely a credential.</summary>
    public static bool LooksSecret(string key)
    {
        var upper = key.Trim().ToUpperInvariant();
        return upper is "AUTHORIZATION" or "PROXY-AUTHORIZATION" or "COOKIE" or "PAT" ||
               SecretKeyMarkers.Any(x => upper.Contains(x, StringComparison.Ordinal));
    }

    private static JsonObject ToObject(IEnumerable<McpConnectorValue> values, bool includeSecrets)
    {
        var result = new JsonObject();
        foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x.Key) && (includeSecrets || !x.IsSecret)))
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

    private static List<McpConnectorValue> GetValues(JsonObject obj, string key, IReadOnlyCollection<string> secretKeys)
    {
        var result = obj[key] is JsonObject map
            ? map.Where(x => x.Value is JsonValue v && v.TryGetValue<string>(out _))
                .Select(x => new McpConnectorValue(x.Key, x.Value!.GetValue<string>(), secretKeys.Contains(x.Key)))
                .ToList()
            : [];

        foreach (var secret in secretKeys.Where(x => result.All(r => r.Key != x)))
            result.Add(new McpConnectorValue(secret, string.Empty, true));
        return result;
    }
}

/// <summary>Secret values of a connector, kept in the OS credential store as one entry per connector.</summary>
public sealed class McpConnectorSecrets
{
    public Dictionary<string, string> Env { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Secret of a confidential OAuth app, only sent when signing in.</summary>
    public string? OAuthClientSecret { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => Env.Count == 0 && Headers.Count == 0 && string.IsNullOrEmpty(OAuthClientSecret);
}
