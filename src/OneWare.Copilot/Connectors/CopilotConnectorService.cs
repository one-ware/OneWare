using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.Logging;
using OneWare.Copilot.Services;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Connectors;

public enum McpVariableSource
{
    Missing,
    Environment,
    OneWare
}

public enum McpConnectorState
{
    Unknown,
    Connected,
    NeedsSignIn,
    Failed
}

public sealed record McpConnectorStatus(McpConnectorState State, string? Message = null);

/// <summary>
/// Manages the IDE-wide MCP servers ("connectors"). Their definitions live in the Copilot CLI's user configuration
/// (<c>~/.copilot/mcp-config.json</c>), so the CLI and OneWare share them. Values stored in OneWare (secrets) are kept
/// in the OS credential store; the definitions reference them with <c>${VAR}</c> and OneWare resolves them only for
/// the connector that references them, never in the runtime's environment, which every MCP server inherits.
/// OAuth sign-ins are run and kept by the runtime in its keychain storage.
/// </summary>
public sealed class CopilotConnectorService
{
    public const string SecretService = "oneware-mcp";

    /// <summary>Redirect URL of the runtime's OAuth sign-in, needed when registering an OAuth app.</summary>
    public const string OAuthRedirectUrl = "http://127.0.0.1:41347/";

    private const string OAuthClientSecretPrefix = "oauth-client-secret:";
    private static readonly TimeSpan HelperIdleTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    private readonly ISecretStore _secretStore;
    private readonly IPaths _paths;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _helperLock = new(1, 1);
    private readonly SemaphoreSlim _variablesLock = new(1, 1);
    private Helper? _helper;
    private CancellationTokenSource? _helperIdleCts;

    public CopilotConnectorService(ISecretStore secretStore, IPaths paths, ILogger logger)
    {
        _secretStore = secretStore;
        _paths = paths;
        _logger = logger;
    }

    /// <summary>Connectors were added, edited, removed, enabled or disabled.</summary>
    public event EventHandler? ConnectorsChanged;

    /// <summary>Values stored in OneWare changed.</summary>
    public event EventHandler? ValuesChanged;

    /// <summary>The user signed in to the named connector.</summary>
    public event EventHandler<string>? SignedIn;

    public bool IsCliInstalled => File.Exists(CopilotModule.GetCliPath(_paths));

    private string VariablesIndexFile => Path.Combine(_paths.AppDataDirectory, "Copilot", "mcp-variables.json");

    private static bool IgnoreCase => OperatingSystem.IsWindows();

    #region Environment

    /// <summary>
    /// Environment for a Copilot CLI process, see <see cref="CopilotRuntimeEnvironment.Merge" />. It never contains
    /// values stored in OneWare.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> GetRuntimeEnvironmentAsync() =>
        CopilotRuntimeEnvironment.Merge(CopilotRuntimeEnvironment.GetProcessEnvironment(),
            await LoginShellEnvironment.GetAsync(), IgnoreCase);

    public async Task<McpVariableSource> GetVariableSourceAsync(string variable)
    {
        if ((await GetStoredVariablesAsync()).Contains(variable)) return McpVariableSource.OneWare;

        var environment = await GetRuntimeEnvironmentAsync();
        return environment.TryGetValue(variable, out var value) && !string.IsNullOrEmpty(value)
            ? McpVariableSource.Environment
            : McpVariableSource.Missing;
    }

    /// <summary>Stores values in the credential store; <see langword="null" /> or empty removes a value.</summary>
    public async Task SetVariablesAsync(IReadOnlyDictionary<string, string?> values)
    {
        if (values.Count == 0) return;

        await _variablesLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                var names = ReadVariablesIndex();
                foreach (var (name, value) in values)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        _secretStore.Remove(SecretService, name);
                        names.Remove(name);
                    }
                    else
                    {
                        _secretStore.Set(SecretService, name, value);
                        names.Add(name);
                    }
                }

                WriteVariablesIndex(names);
            });
        }
        finally
        {
            _variablesLock.Release();
        }

        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves stored values to other variable names, e.g. after renaming a connector.</summary>
    public async Task RenameVariablesAsync(IReadOnlyDictionary<string, string> renames)
    {
        var values = await Task.Run(() =>
        {
            var result = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (from, to) in renames)
            {
                if (from == to || _secretStore.Get(SecretService, from) is not { Length: > 0 } value) continue;
                result[to] = value;
                result.TryAdd(from, null);
            }

            return result;
        });
        await SetVariablesAsync(values);
    }

    public async Task<IReadOnlySet<string>> GetStoredVariablesAsync()
    {
        await _variablesLock.WaitAsync();
        try
        {
            return await Task.Run(ReadVariablesIndex);
        }
        finally
        {
            _variablesLock.Release();
        }
    }

    public Task<bool> HasOAuthClientSecretAsync(string connector) =>
        Task.Run(() => !string.IsNullOrEmpty(_secretStore.Get(SecretService, OAuthClientSecretPrefix + connector)));

    /// <summary>Stores the secret of a confidential OAuth app; <see langword="null" /> or empty removes it.</summary>
    public Task SetOAuthClientSecretAsync(string connector, string? secret) => Task.Run(() =>
    {
        if (string.IsNullOrEmpty(secret)) _secretStore.Remove(SecretService, OAuthClientSecretPrefix + connector);
        else _secretStore.Set(SecretService, OAuthClientSecretPrefix + connector, secret);
    });

    /// <summary>
    /// The connector with the values stored in OneWare filled in. Other <c>${VAR}</c> references stay for the runtime
    /// to expand from its environment.
    /// </summary>
    public async Task<McpConnector> ResolveAsync(McpConnector connector)
    {
        var stored = await GetStoredVariablesAsync();
        var names = connector.GetVariables().Where(stored.Contains).ToList();
        if (names.Count == 0) return connector;

        var values = await Task.Run(() => names
            .Select(x => (Name: x, Value: _secretStore.Get(SecretService, x)))
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .ToDictionary(x => x.Name, x => x.Value!, StringComparer.Ordinal));
        return connector.WithVariables(values);
    }

    /// <summary>
    /// Enabled connectors that use values stored in OneWare, resolved. Chats start these themselves instead of
    /// letting the runtime start them from the configuration with the values missing. Reads the configuration file
    /// directly, so no runtime has to be started for it.
    /// </summary>
    public async Task<IReadOnlyList<McpConnector>> GetResolvedConnectorsAsync()
    {
        var stored = await GetStoredVariablesAsync();
        if (stored.Count == 0) return [];

        var result = new List<McpConnector>();
        foreach (var connector in await GetConfiguredConnectorsAsync())
            if (connector.Enabled && connector.GetVariables().Any(stored.Contains))
                result.Add(await ResolveAsync(connector));
        return result;
    }

    // The credential store can't list entries, so the names (not the values) of stored variables are kept here.
    private HashSet<string> ReadVariablesIndex()
    {
        try
        {
            if (File.Exists(VariablesIndexFile) &&
                JsonSerializer.Deserialize<string[]>(File.ReadAllText(VariablesIndexFile)) is { } names)
                return new HashSet<string>(names, StringComparer.Ordinal);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(e, "Failed to read {File}.", VariablesIndexFile);
        }

        return new HashSet<string>(StringComparer.Ordinal);
    }

    private void WriteVariablesIndex(IEnumerable<string> names)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(VariablesIndexFile)!);
        File.WriteAllText(VariablesIndexFile, JsonSerializer.Serialize(names.Order(StringComparer.Ordinal)));
    }

    #endregion

    #region Configuration

    public async Task<IReadOnlyList<McpConnector>> ListAsync(CancellationToken cancellationToken = default)
    {
        var list = await UseHelperAsync(helper => helper.Client.Rpc.Mcp.Config.ListAsync(cancellationToken));
        var disabled = ReadDisabledServers();

        return list.Servers
            .Select(x => JsonNode.Parse(x.Value.GetRawText()) is JsonObject config
                ? McpConnector.FromConfig(x.Key, config, !disabled.Contains(x.Key))
                : null)
            .OfType<McpConnector>()
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Adds or updates a connector; <paramref name="previousName" /> renames an existing one.</summary>
    public async Task SaveAsync(McpConnector connector, string? previousName = null)
    {
        await UseHelperAsync(async helper =>
        {
            var config = helper.Client.Rpc.Mcp.Config;
            var existing = (await config.ListAsync()).Servers.Keys.ToHashSet(StringComparer.Ordinal);
            var json = JsonSerializer.SerializeToElement(connector.ToConfig());

            if (previousName != null && previousName != connector.Name && existing.Contains(previousName))
                await config.RemoveAsync(previousName);

            if (existing.Contains(connector.Name)) await config.UpdateAsync(connector.Name, json);
            else await config.AddAsync(connector.Name, json);

            if (connector.Enabled) await config.EnableAsync([connector.Name]);
            else await config.DisableAsync([connector.Name]);
            return true;
        });

        if (previousName != null && previousName != connector.Name)
            await MoveOAuthClientSecretAsync(previousName, connector.Name);

        ConnectorsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetEnabledAsync(string name, bool enabled)
    {
        await UseHelperAsync(async helper =>
        {
            if (enabled) await helper.Client.Rpc.Mcp.Config.EnableAsync([name]);
            else await helper.Client.Rpc.Mcp.Config.DisableAsync([name]);
            return true;
        });
        ConnectorsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes the connector and the values OneWare stored for it.</summary>
    public async Task RemoveAsync(McpConnector connector)
    {
        await UseHelperAsync(async helper =>
        {
            // Also drops the name from the CLI's disabled list, so a new connector with this name starts enabled.
            await helper.Client.Rpc.Mcp.Config.EnableAsync([connector.Name]);
            await helper.Client.Rpc.Mcp.Config.RemoveAsync(connector.Name);
            return true;
        });

        await SetOAuthClientSecretAsync(connector.Name, null);

        var prefix = McpConnector.GetSecretVariablePrefix(connector.Name);
        var stored = await GetStoredVariablesAsync();
        var owned = connector.GetVariables().Where(x => x.StartsWith(prefix, StringComparison.Ordinal) &&
                                                         stored.Contains(x)).ToList();
        if (owned.Count > 0) await SetVariablesAsync(owned.ToDictionary(x => x, string? (_) => null));

        ConnectorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task MoveOAuthClientSecretAsync(string from, string to)
    {
        var secret = await Task.Run(() => _secretStore.Get(SecretService, OAuthClientSecretPrefix + from));
        if (string.IsNullOrEmpty(secret)) return;
        await SetOAuthClientSecretAsync(to, secret);
        await SetOAuthClientSecretAsync(from, null);
    }

    /// <summary>The connectors as configured, read from the configuration file without starting a runtime.</summary>
    public Task<List<McpConnector>> GetConfiguredConnectorsAsync() => Task.Run(ReadConfigurationFile);

    private List<McpConnector> ReadConfigurationFile()
    {
        var file = Path.Combine(CopilotSessionConfiguration.GetConfigDirectory(), "mcp-config.json");
        try
        {
            if (!File.Exists(file) ||
                JsonNode.Parse(File.ReadAllText(file), documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                })?["mcpServers"] is not JsonObject servers)
                return [];

            var disabled = ReadDisabledServers();
            return servers.Where(x => x.Value is JsonObject)
                .Select(x => McpConnector.FromConfig(x.Key, (JsonObject)x.Value!, !disabled.Contains(x.Key)))
                .ToList();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(e, "Failed to read MCP servers from {File}.", file);
            return [];
        }
    }

    // The configuration API doesn't report whether a server is disabled; the CLI keeps that in its settings.
    private HashSet<string> ReadDisabledServers()
    {
        var file = Path.Combine(CopilotSessionConfiguration.GetConfigDirectory(), "settings.json");
        try
        {
            if (File.Exists(file) &&
                JsonNode.Parse(File.ReadAllText(file), documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                })?["disabledMcpServers"] is JsonArray names)
                return names.OfType<JsonValue>().Select(x => x.TryGetValue<string>(out var s) ? s : null)
                    .OfType<string>().ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(e, "Failed to read disabled MCP servers from {File}.", file);
        }

        return new HashSet<string>(StringComparer.Ordinal);
    }

    #endregion

    #region Status and sign-in

    /// <summary>Starts the connector in a separate session to see whether it connects. Local ones run their command.</summary>
    public Task<McpConnectorStatus> CheckAsync(McpConnector connector, CancellationToken cancellationToken = default) =>
        UseHelperAsync(async helper =>
        {
            var session = await helper.GetSessionAsync();
            try
            {
                return await StartAsync(session, connector, cancellationToken);
            }
            finally
            {
                try
                {
                    await session.Rpc.Mcp.StopServerAsync(connector.Name, CancellationToken.None);
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Failed to stop MCP server {Name} after checking it.", connector.Name);
                }
            }
        });

    /// <summary>
    /// Runs the OAuth sign-in of a remote connector in the browser and waits until it connects. Tokens are kept by
    /// the runtime, so every chat uses them afterwards.
    /// </summary>
    public async Task<McpConnectorStatus> SignInAsync(McpConnector connector,
        CancellationToken cancellationToken = default)
    {
        // A sign-in waits for the browser, so it gets its own runtime instead of blocking the shared helper; the
        // runtime serves the OAuth redirect and must stay alive until then.
        await using var helper = await Helper.CreateAsync(this);
        var session = await helper.GetSessionAsync();

        var status = await StartAsync(session, connector, cancellationToken);
        if (status.State != McpConnectorState.NeedsSignIn) return status;

        var connected = new TaskCompletionSource<McpConnectorStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = session.On<SessionMcpServerStatusChangedEvent>(e =>
        {
            if (e.Data.ServerName != connector.Name) return;
            if (e.Data.Status == McpServerStatus.Connected)
                connected.TrySetResult(new McpConnectorStatus(McpConnectorState.Connected));
            else if (e.Data.Status == McpServerStatus.Failed)
                connected.TrySetResult(new McpConnectorStatus(McpConnectorState.Failed, e.Data.Error));
        });

        var clientSecret = await Task.Run(() =>
            _secretStore.Get(SecretService, OAuthClientSecretPrefix + connector.Name));
        McpOauthLoginResult login;
        try
        {
            login = await session.Rpc.Mcp.Oauth.LoginAsync(new McpOauthLoginRequest
            {
                ServerName = connector.Name,
                ClientName = "OneWare Studio",
                CallbackSuccessMessage = "Signed in. You can close this tab and return to OneWare Studio.",
                ClientId = string.IsNullOrWhiteSpace(connector.OAuthClientId) ? null : connector.OAuthClientId,
                ClientSecret = string.IsNullOrEmpty(clientSecret) ? null : clientSecret,
                PublicClient = connector.OAuthPublicClient ?? (string.IsNullOrEmpty(clientSecret) ? null : false)
            }, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Failed to sign in to MCP server {Name}.", connector.Name);
            return new McpConnectorStatus(McpConnectorState.Failed, DescribeSignInError(e.Message));
        }

        // Without a URL, stored tokens were still valid and the server reconnects by itself.
        if (!string.IsNullOrEmpty(login.AuthorizationUrl)) PlatformHelper.OpenHyperLink(login.AuthorizationUrl);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SignInTimeout);
        try
        {
            status = await connected.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            status = await GetServerStatusAsync(session, connector.Name, CancellationToken.None);
            if (status.State != McpConnectorState.Connected)
                return new McpConnectorStatus(McpConnectorState.NeedsSignIn, "The sign-in was not completed.");
        }

        if (status.State == McpConnectorState.Connected) SignedIn?.Invoke(this, connector.Name);
        return status;
    }

    public static string DescribeSignInError(string message) =>
        message.Contains("dynamic client registration", StringComparison.OrdinalIgnoreCase)
            ? "The server doesn't let apps register themselves. Register an OAuth app with the provider, using " +
              $"{OAuthRedirectUrl} as redirect URL, and enter its client ID (and secret) in the connector."
            : message;

    internal static bool IsAuthenticationRequired(string message) =>
        message.Contains("requires authentication", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("needs-auth", StringComparison.OrdinalIgnoreCase);

    private async Task<McpConnectorStatus> StartAsync(CopilotSession session, McpConnector connector,
        CancellationToken cancellationToken)
    {
        try
        {
            await session.Rpc.Mcp.StartServerAsync(new McpStartServerRequest
            {
                ServerName = connector.Name,
                Config = JsonSerializer.SerializeToElement((await ResolveAsync(connector)).ToConfig())
            }, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            if (IsAuthenticationRequired(e.Message)) return new McpConnectorStatus(McpConnectorState.NeedsSignIn);
            return new McpConnectorStatus(McpConnectorState.Failed, StripRpcPrefix(e.Message));
        }

        return await GetServerStatusAsync(session, connector.Name, cancellationToken);
    }

    private static async Task<McpConnectorStatus> GetServerStatusAsync(CopilotSession session, string name,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StartTimeout);
        try
        {
            while (true)
            {
                var server = (await session.Rpc.Mcp.ListAsync(timeout.Token)).Servers.FirstOrDefault(x => x.Name == name);
                if (server == null) return new McpConnectorStatus(McpConnectorState.Unknown);
                if (server.Status == McpServerStatus.Connected)
                    return new McpConnectorStatus(McpConnectorState.Connected);
                if (server.Status == McpServerStatus.NeedsAuth)
                    return new McpConnectorStatus(McpConnectorState.NeedsSignIn);
                if (server.Status == McpServerStatus.Failed)
                    return new McpConnectorStatus(McpConnectorState.Failed, server.Error);
                if (server.Status != McpServerStatus.Pending)
                    return new McpConnectorStatus(McpConnectorState.Unknown, server.Status.ToString());
                await Task.Delay(250, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new McpConnectorStatus(McpConnectorState.Failed, "The server did not start in time.");
        }
    }

    private static string StripRpcPrefix(string message)
    {
        const string marker = "failed with message: ";
        var index = message.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? message : message[(index + marker.Length)..];
    }

    #endregion

    #region Helper runtime

    /// <summary>
    /// Runs <paramref name="action" /> on a shared, short-lived CLI process. It is shut down when idle, so it never
    /// holds the CLI executable while it gets updated.
    /// </summary>
    private async Task<T> UseHelperAsync<T>(Func<Helper, Task<T>> action)
    {
        if (!IsCliInstalled)
            throw new InvalidOperationException("The Copilot CLI is not installed. Open the AI chat to install it.");

        await _helperLock.WaitAsync();
        try
        {
            _helperIdleCts?.Cancel();
            _helper ??= await Helper.CreateAsync(this);
            try
            {
                return await action(_helper);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // The process may have exited; the next operation starts a new one.
                await _helper.DisposeAsync();
                _helper = null;
                throw;
            }
        }
        finally
        {
            var idle = new CancellationTokenSource();
            _helperIdleCts = idle;
            _helperLock.Release();
            _ = Task.Delay(HelperIdleTimeout, idle.Token).ContinueWith(_ => ResetHelperAsync(idle.Token),
                TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    private async Task ResetHelperAsync(CancellationToken idleToken = default)
    {
        await _helperLock.WaitAsync(CancellationToken.None);
        try
        {
            if (idleToken.IsCancellationRequested || _helper == null) return;
            await _helper.DisposeAsync();
            _helper = null;
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Failed to stop the connector helper runtime.");
        }
        finally
        {
            _helperLock.Release();
        }
    }

    private sealed class Helper : IAsyncDisposable
    {
        private CopilotSession? _session;

        private Helper(CopilotClient client)
        {
            Client = client;
        }

        public CopilotClient Client { get; }

        public static async Task<Helper> CreateAsync(CopilotConnectorService service)
        {
            var connection = RuntimeConnection.ForStdio(CopilotModule.GetCliPath(service._paths), []);
            connection.Environment = await GetRuntimeEnvironmentAsync();

            // Default mode with the user's configuration directory, so the configuration API edits
            // ~/.copilot/mcp-config.json; no Copilot account is needed for that.
            var client = new CopilotClient(new CopilotClientOptions
            {
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Connection = connection,
                UseLoggedInUser = false
            });

            try
            {
                await client.StartAsync();
            }
            catch
            {
                await client.DisposeAsync();
                throw;
            }

            return new Helper(client);
        }

        /// <summary>A session without config discovery, used to start individual connectors.</summary>
        public async Task<CopilotSession> GetSessionAsync() => _session ??= await Client.CreateSessionAsync(
            new SessionConfig
            {
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ClientName = "OneWare Studio",
                EnableConfigDiscovery = false,
                AvailableTools = new ToolSet().AddMcp("*"),
                McpOAuthTokenStorage = McpOAuthTokenStorageMode.Persistent,
                OnPermissionRequest = (_, _) =>
                    Task.FromResult(PermissionDecision.Reject("Connector checks don't run tools."))
            });

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_session != null) await _session.DisposeAsync();
            }
            catch
            {
                // The process is stopped below anyway.
            }

            await Client.DisposeAsync();
        }
    }

    #endregion
}
