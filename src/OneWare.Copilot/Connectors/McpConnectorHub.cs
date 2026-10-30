using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.Logging;
using OneWare.Copilot.Services;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Connectors;

public enum McpConnectorState
{
    Unknown,
    Pending,
    Connected,
    NeedsSignIn,
    Failed
}

public sealed record McpConnectorStatus(McpConnectorState State, string? Message = null);

/// <summary>An enabled connector ready to be started in a session, with its secret values filled in.</summary>
/// <param name="Config">Configuration in the CLI format, for <c>session.Rpc.Mcp.StartServerAsync</c>.</param>
/// <param name="ServerConfig">The same as session configuration; <see langword="null" /> for SSE servers.</param>
/// <param name="Fingerprint">Changes whenever the configuration does, to restart the server.</param>
public sealed record ResolvedMcpConnector(string Name, JsonObject Config, McpServerConfig? ServerConfig,
    string Fingerprint);

/// <summary>
/// Owns the IDE-wide MCP servers ("connectors"). They are kept in OneWare's <c>mcp.json</c>, which may also be
/// edited by hand, and their secret values in the OS credential store (one entry per connector). Chats start the
/// enabled connectors in their sessions and report the status back here, so every view shows the same state.
/// OAuth tokens are kept by the runtime in its keychain storage.
/// </summary>
public sealed class McpConnectorHub : IDisposable
{
    public const string SecretService = "oneware-mcp";

    /// <summary>Redirect URL of the runtime's OAuth sign-in, needed when registering an OAuth app.</summary>
    public const string OAuthRedirectUrl = "http://127.0.0.1:41347/";

    private static readonly TimeSpan HelperIdleTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ChangeDelay = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ISecretStore _secretStore;
    private readonly IPaths _paths;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly SemaphoreSlim _helperLock = new(1, 1);
    // The runtime serves every OAuth redirect on the same port, so only one sign-in can run at a time.
    private readonly SemaphoreSlim _signInLock = new(1, 1);
    private readonly Lock _stateLock = new();
    private readonly Dictionary<string, McpConnectorSecrets> _secrets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, McpConnectorStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<McpConnector>? _connectors;
    private string? _lastContent;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _reloadCts;
    private CancellationTokenSource? _changedCts;
    private Helper? _helper;
    private CancellationTokenSource? _helperIdleCts;

    public McpConnectorHub(ISecretStore secretStore, IPaths paths, ILogger logger)
    {
        _secretStore = secretStore;
        _paths = paths;
        _logger = logger;
    }

    /// <summary>Connectors were added, edited, removed, enabled or disabled, also by editing the file.</summary>
    public event EventHandler? Changed;

    /// <summary>The status of the named server changed.</summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>The user signed in to the named server; sessions reconnect it.</summary>
    public event EventHandler<string>? SignedIn;

    public string ConfigFile => Path.Combine(_paths.AppDataDirectory, "Copilot", "mcp.json");

    /// <summary>Why <see cref="ConfigFile" /> could not be read, if it can't; the last valid state stays in use.</summary>
    public string? LoadError { get; private set; }

    public bool IsCliInstalled => File.Exists(CopilotModule.GetCliPath(_paths));

    private static bool IgnoreCase => OperatingSystem.IsWindows();

    /// <summary>
    /// Environment for a Copilot CLI process, see <see cref="CopilotRuntimeEnvironment.Merge" />. <c>${VAR}</c>
    /// references in connectors are expanded from it. It never contains secret values.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> GetRuntimeEnvironmentAsync() =>
        CopilotRuntimeEnvironment.Merge(CopilotRuntimeEnvironment.GetProcessEnvironment(),
            await LoginShellEnvironment.GetAsync(), IgnoreCase);

    public void Dispose()
    {
        _watcher?.Dispose();
        _reloadCts?.Cancel();
        _changedCts?.Cancel();
        _helperIdleCts?.Cancel();
        _ = ResetHelperAsync();
    }

    #region Configuration

    /// <summary>The connectors without secret values, sorted by name.</summary>
    public async Task<IReadOnlyList<McpConnector>> GetConnectorsAsync()
    {
        if (_connectors is { } cached) return cached;

        await _fileLock.WaitAsync();
        try
        {
            if (_connectors == null) await LoadAsync();
            StartWatching();
            return _connectors!;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<McpConnector?> FindAsync(string name) =>
        (await GetConnectorsAsync()).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Secret values of the connector, from the OS credential store.</summary>
    public async Task<McpConnectorSecrets> GetSecretsAsync(string id)
    {
        lock (_stateLock)
        {
            if (_secrets.TryGetValue(id, out var cached)) return cached;
        }

        var secrets = await Task.Run(() => ReadSecrets(id));
        lock (_stateLock)
        {
            _secrets[id] = secrets;
        }

        return secrets;
    }

    /// <summary>
    /// Adds or updates a connector (matched by <see cref="McpConnector.Id" />) and stores its secret values, which
    /// the connector must contain.
    /// </summary>
    public async Task SaveAsync(McpConnector connector, string? oauthClientSecret)
    {
        if (!McpConnector.IsValidName(connector.Name))
            throw new ArgumentException($"'{connector.Name}' is not a valid name.", nameof(connector));

        await EditAsync(servers =>
        {
            var existing = servers.FirstOrDefault(x => GetId(x.Value) == connector.Id).Key;
            if (servers.Any(x => x.Key != existing &&
                                 string.Equals(x.Key, connector.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A connector named '{connector.Name}' already exists.");

            // Replacing the entry in place keeps the order of the file.
            if (existing != null && existing != connector.Name) Rename(servers, existing, connector.Name);
            servers[connector.Name] = connector.ToStoredConfig();
        });

        await WriteSecretsAsync(connector.Id, connector.GetSecrets(oauthClientSecret));
    }

    /// <summary>Adds connectors, skipping names that exist. Returns the names that were added.</summary>
    public async Task<IReadOnlyList<string>> AddAsync(IEnumerable<ImportedMcpConnector> connectors,
        Action<string> warn)
    {
        var added = new List<ImportedMcpConnector>();
        await EditAsync(servers =>
        {
            foreach (var imported in connectors)
            {
                var name = imported.Connector.Name;
                if (servers.Any(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)))
                {
                    warn($"'{name}' already exists and was skipped.");
                    continue;
                }

                servers[name] = imported.Connector.ToStoredConfig();
                added.Add(imported);
            }
        });

        foreach (var imported in added)
            await WriteSecretsAsync(imported.Connector.Id, imported.Connector.GetSecrets(imported.OAuthClientSecret));

        return added.Select(x => x.Connector.Name).ToList();
    }

    public Task SetEnabledAsync(string id, bool enabled) => EditAsync(servers =>
    {
        if (servers.FirstOrDefault(x => GetId(x.Value) == id).Value is not JsonObject config) return;
        if (enabled) config.Remove(McpConnector.DisabledField);
        else config[McpConnector.DisabledField] = true;
    });

    /// <summary>Removes the connector and its secret values.</summary>
    public async Task RemoveAsync(string id)
    {
        string? name = null;
        await EditAsync(servers =>
        {
            name = servers.FirstOrDefault(x => GetId(x.Value) == id).Key;
            if (name != null) servers.Remove(name);
        });

        await WriteSecretsAsync(id, new McpConnectorSecrets());
        if (name != null) ReportStatus(name, null);
    }

    /// <summary>
    /// The enabled connectors with their secret values filled in. <c>${VAR}</c> references stay for the runtime to
    /// expand from its environment.
    /// </summary>
    public async Task<IReadOnlyList<ResolvedMcpConnector>> ResolveEnabledAsync()
    {
        var result = new List<ResolvedMcpConnector>();
        foreach (var connector in (await GetConnectorsAsync()).Where(x => x.Enabled))
        {
            var resolved = await ResolveAsync(connector);
            var config = resolved.ToCliConfig();
            result.Add(new ResolvedMcpConnector(connector.Name, config, resolved.ToServerConfig(),
                Fingerprint(config)));
        }

        return result;
    }

    /// <summary>The connector with its secret values filled in.</summary>
    public async Task<McpConnector> ResolveAsync(McpConnector connector) =>
        connector.WithSecrets(await GetSecretsAsync(connector.Id));

    /// <summary>
    /// Servers in the Copilot CLI's user configuration (<c>~/.copilot/mcp-config.json</c>), offered for import.
    /// Chats don't start them; OneWare's own list is the only source of IDE-wide servers.
    /// </summary>
    public async Task<IReadOnlyList<ImportedMcpConnector>> ReadCopilotCliServersAsync(Action<string>? warn = null)
    {
        var file = Path.Combine(CopilotSessionConfiguration.GetConfigDirectory(), "mcp-config.json");
        try
        {
            if (!File.Exists(file)) return [];
            var text = await File.ReadAllTextAsync(file);
            return McpConnectorImport.ParseText(text, warn ?? (_ => { }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(e, "Failed to read {File}.", file);
            return [];
        }
    }

    /// <summary>Names of the servers in the Copilot CLI's user configuration, which chats leave out.</summary>
    public async Task<IReadOnlyList<string>> GetCopilotCliServerNamesAsync()
    {
        var file = Path.Combine(CopilotSessionConfiguration.GetConfigDirectory(), "mcp-config.json");
        try
        {
            if (!File.Exists(file)) return [];
            return JsonNode.Parse(await File.ReadAllTextAsync(file), documentOptions: ReadOptions)?["mcpServers"]
                is JsonObject servers
                ? servers.Select(x => x.Key).ToList()
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogDebug(e, "Failed to read {File}.", file);
            return [];
        }
    }

    private async Task EditAsync(Action<JsonObject> edit)
    {
        await _fileLock.WaitAsync();
        try
        {
            var root = await ReadRootAsync() ?? throw new InvalidOperationException(
                $"{Path.GetFileName(ConfigFile)} can't be read: {LoadError} Fix or remove the file first.");
            if (root["mcpServers"] is not JsonObject servers) root["mcpServers"] = servers = new JsonObject();

            edit(servers);
            await WriteRootAsync(root);
            ApplyContent(root);
            StartWatching();
        }
        finally
        {
            _fileLock.Release();
        }

        RaiseChanged();
    }

    /// <summary>Reads the file; <see langword="null" /> if it has errors.</summary>
    private async Task<JsonObject?> ReadRootAsync()
    {
        string text;
        try
        {
            text = File.Exists(ConfigFile) ? await File.ReadAllTextAsync(ConfigFile) : string.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LoadError = e.Message;
            return null;
        }

        _lastContent = text;
        if (string.IsNullOrWhiteSpace(text))
        {
            LoadError = null;
            return new JsonObject { ["mcpServers"] = new JsonObject() };
        }

        try
        {
            if (JsonNode.Parse(text, documentOptions: ReadOptions) is JsonObject root &&
                root["mcpServers"] is null or JsonObject)
            {
                LoadError = null;
                return root;
            }

            LoadError = "It must contain an object with an \"mcpServers\" object.";
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            LoadError = e.Message;
        }

        return null;
    }

    private async Task WriteRootAsync(JsonObject root)
    {
        var text = root.ToJsonString(WriteOptions) + Environment.NewLine;
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);
        // Written next to the file and moved over it, so a crash never leaves a half-written file.
        var temp = ConfigFile + ".tmp";
        await File.WriteAllTextAsync(temp, text);
        File.Move(temp, ConfigFile, true);
        _lastContent = text;
    }

    private async Task LoadAsync()
    {
        var root = await ReadRootAsync();
        if (root == null)
        {
            _logger.LogWarning("Failed to read {File}: {Error}", ConfigFile, LoadError);
            _connectors ??= [];
            return;
        }

        // Entries added by hand get an id, which their secret values are stored under.
        var servers = root["mcpServers"] as JsonObject;
        var missingIds = servers?.Where(x => x.Value is JsonObject config && string.IsNullOrEmpty(GetId(config)))
            .Select(x => (JsonObject)x.Value!).ToList() ?? [];
        if (missingIds.Count > 0)
        {
            foreach (var config in missingIds) config[McpConnector.IdField] = Guid.NewGuid().ToString("N");
            try
            {
                await WriteRootAsync(root);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(e, "Failed to write {File}.", ConfigFile);
            }
        }

        ApplyContent(root);
    }

    private void ApplyContent(JsonObject root)
    {
        var result = new List<McpConnector>();
        if (root["mcpServers"] is JsonObject servers)
            foreach (var (name, node) in servers)
            {
                if (node is not JsonObject config) continue;
                if (!McpConnector.IsValidName(name))
                {
                    _logger.LogWarning("Ignoring MCP server '{Name}' in {File}: invalid name.", name, ConfigFile);
                    continue;
                }

                if (result.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Ignoring MCP server '{Name}' in {File}: duplicate name.", name, ConfigFile);
                    continue;
                }

                result.Add(McpConnector.FromConfig(name, config));
            }

        _connectors = result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void StartWatching()
    {
        if (_watcher != null) return;
        try
        {
            var directory = Path.GetDirectoryName(ConfigFile)!;
            Directory.CreateDirectory(directory);
            _watcher = new FileSystemWatcher(directory, Path.GetFileName(ConfigFile))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Failed to watch {File}.", ConfigFile);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _reloadCts, cts)?.Cancel();
        _ = Task.Delay(ChangeDelay, cts.Token).ContinueWith(_ => ReloadFromDiskAsync(),
            TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    private async Task ReloadFromDiskAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            string text;
            try
            {
                text = File.Exists(ConfigFile) ? await File.ReadAllTextAsync(ConfigFile) : string.Empty;
            }
            catch (IOException)
            {
                // Still being written; the watcher reports the next write.
                return;
            }

            // Our own writes.
            if (text == _lastContent) return;
            await LoadAsync();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to reload {File}.", ConfigFile);
            return;
        }
        finally
        {
            _fileLock.Release();
        }

        lock (_stateLock)
        {
            _secrets.Clear();
        }

        RaiseChanged();
    }

    private void RaiseChanged()
    {
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _changedCts, cts)?.Cancel();
        _ = Task.Delay(ChangeDelay, cts.Token).ContinueWith(_ => Changed?.Invoke(this, EventArgs.Empty),
            TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    private static string? GetId(JsonNode? config) =>
        config?[McpConnector.IdField] is JsonValue value && value.TryGetValue<string>(out var id) ? id : null;

    private static void Rename(JsonObject servers, string from, string to)
    {
        var entries = servers.ToList();
        servers.Clear();
        foreach (var (key, value) in entries) servers[key == from ? to : key] = value;
    }

    private static string Fingerprint(JsonObject config) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.ToJsonString())));

    #endregion

    #region Secrets

    private McpConnectorSecrets ReadSecrets(string id)
    {
        try
        {
            if (_secretStore.Get(SecretService, id) is { Length: > 0 } json &&
                JsonSerializer.Deserialize<McpConnectorSecrets>(json) is { } secrets)
                return secrets;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to read the secrets of MCP server {Id}.", id);
        }

        return new McpConnectorSecrets();
    }

    private async Task WriteSecretsAsync(string id, McpConnectorSecrets secrets)
    {
        await Task.Run(() =>
        {
            if (secrets.IsEmpty) _secretStore.Remove(SecretService, id);
            else _secretStore.Set(SecretService, id, JsonSerializer.Serialize(secrets));
        });

        lock (_stateLock)
        {
            _secrets[id] = secrets;
        }
    }

    #endregion

    #region Status and sign-in

    public McpConnectorStatus? GetStatus(string name)
    {
        lock (_stateLock)
        {
            return _statuses.GetValueOrDefault(name);
        }
    }

    /// <summary>Records the status of a server, as seen by a chat or a check.</summary>
    public void ReportStatus(string name, McpConnectorStatus? status)
    {
        lock (_stateLock)
        {
            if (Equals(_statuses.GetValueOrDefault(name), status)) return;
            if (status == null) _statuses.Remove(name);
            else _statuses[name] = status;
        }

        StatusChanged?.Invoke(this, name);
    }

    public static McpConnectorStatus ToStatus(McpServerStatus status, string? error)
    {
        if (status == McpServerStatus.Connected) return new McpConnectorStatus(McpConnectorState.Connected);
        if (status == McpServerStatus.NeedsAuth) return new McpConnectorStatus(McpConnectorState.NeedsSignIn);
        if (status == McpServerStatus.Failed) return new McpConnectorStatus(McpConnectorState.Failed, error);
        if (status == McpServerStatus.Pending) return new McpConnectorStatus(McpConnectorState.Pending);
        return new McpConnectorStatus(McpConnectorState.Unknown);
    }

    /// <summary>
    /// Starts the connector in a separate session to see whether it connects. Local ones run their command. The
    /// connector must contain its secret values.
    /// </summary>
    public async Task<McpConnectorStatus> CheckAsync(McpConnector connector, bool report,
        CancellationToken cancellationToken = default)
    {
        var status = await UseHelperAsync(async helper =>
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

        if (report) ReportStatus(connector.Name, status);
        return status;
    }

    /// <summary>
    /// Runs the OAuth sign-in of a saved connector in the browser and waits until it connects. Tokens are kept by
    /// the runtime, so every chat uses them afterwards.
    /// </summary>
    public async Task<McpConnectorStatus> SignInAsync(string name, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(name) is not { } connector)
            return new McpConnectorStatus(McpConnectorState.Failed, $"'{name}' is not configured.");

        var secrets = await GetSecretsAsync(connector.Id);
        var resolved = connector.WithSecrets(secrets);

        var status = await RunSignInAsync(async () =>
        {
            // A sign-in waits for the browser, so it gets its own runtime instead of blocking the shared helper; the
            // runtime serves the OAuth redirect and must stay alive until then.
            await using var helper = await Helper.CreateAsync(this);
            var session = await helper.GetSessionAsync();

            var started = await StartAsync(session, resolved, cancellationToken);
            if (started.State != McpConnectorState.NeedsSignIn) return started;

            return await LoginAsync(session, resolved.Name, resolved.OAuthClientId, secrets.OAuthClientSecret,
                resolved.OAuthPublicClient, cancellationToken);
        }, cancellationToken);

        ReportStatus(name, status);
        if (status.State == McpConnectorState.Connected) SignedIn?.Invoke(this, name);
        return status;
    }

    /// <summary>
    /// Runs the OAuth sign-in of a server in a chat's session, e.g. one from a project's <c>.mcp.json</c>.
    /// </summary>
    public async Task<McpConnectorStatus> SignInAsync(CopilotSession session, string name,
        CancellationToken cancellationToken = default)
    {
        var connector = await FindAsync(name);
        var secrets = connector == null ? null : await GetSecretsAsync(connector.Id);
        var status = await RunSignInAsync(() => LoginAsync(session, name, connector?.OAuthClientId,
            secrets?.OAuthClientSecret, connector?.OAuthPublicClient, cancellationToken), cancellationToken);

        ReportStatus(name, status);
        if (status.State == McpConnectorState.Connected) SignedIn?.Invoke(this, name);
        return status;
    }

    private async Task<McpConnectorStatus> RunSignInAsync(Func<Task<McpConnectorStatus>> signIn,
        CancellationToken cancellationToken)
    {
        if (!await _signInLock.WaitAsync(TimeSpan.Zero, cancellationToken))
            return new McpConnectorStatus(McpConnectorState.NeedsSignIn,
                "Another sign-in is in progress. Finish it in the browser first.");
        try
        {
            return await signIn();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "MCP sign-in failed.");
            return new McpConnectorStatus(McpConnectorState.Failed, DescribeSignInError(StripRpcPrefix(e.Message)));
        }
        finally
        {
            _signInLock.Release();
        }
    }

    private async Task<McpConnectorStatus> LoginAsync(CopilotSession session, string name, string? clientId,
        string? clientSecret, bool? publicClient, CancellationToken cancellationToken)
    {
        var connected = new TaskCompletionSource<McpConnectorStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = session.On<SessionMcpServerStatusChangedEvent>(e =>
        {
            if (e.Data.ServerName != name) return;
            if (e.Data.Status == McpServerStatus.Connected)
                connected.TrySetResult(new McpConnectorStatus(McpConnectorState.Connected));
            else if (e.Data.Status == McpServerStatus.Failed)
                connected.TrySetResult(new McpConnectorStatus(McpConnectorState.Failed, e.Data.Error));
        });

        var login = await session.Rpc.Mcp.Oauth.LoginAsync(new McpOauthLoginRequest
        {
            ServerName = name,
            ClientName = "OneWare Studio",
            CallbackSuccessMessage = "Signed in. You can close this tab and return to OneWare Studio.",
            ClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId,
            ClientSecret = string.IsNullOrEmpty(clientSecret) ? null : clientSecret,
            PublicClient = publicClient ?? (string.IsNullOrEmpty(clientSecret) ? null : false)
        }, cancellationToken);

        // Without a URL, stored tokens were still valid and the server reconnects by itself.
        if (!string.IsNullOrEmpty(login.AuthorizationUrl)) PlatformHelper.OpenHyperLink(login.AuthorizationUrl);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SignInTimeout);
        try
        {
            return await connected.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var status = await GetServerStatusAsync(session, name, CancellationToken.None);
            return status.State == McpConnectorState.Connected
                ? status
                : new McpConnectorStatus(McpConnectorState.NeedsSignIn, "The sign-in was not completed.");
        }
    }

    public static string DescribeSignInError(string message) =>
        message.Contains("dynamic client registration", StringComparison.OrdinalIgnoreCase)
            ? "The server doesn't let apps register themselves. Register an OAuth app with the provider, using " +
              $"{OAuthRedirectUrl} as redirect URL, and enter its client ID (and secret) under Advanced."
            : message;

    internal static bool IsDynamicRegistrationError(string? message) =>
        message?.Contains("register themselves", StringComparison.Ordinal) == true ||
        message?.Contains("dynamic client registration", StringComparison.OrdinalIgnoreCase) == true;

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
                Config = JsonSerializer.SerializeToElement(connector.ToCliConfig())
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
                if (server.Status != McpServerStatus.Pending) return ToStatus(server.Status, server.Error);
                await Task.Delay(250, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new McpConnectorStatus(McpConnectorState.Failed, "The server did not start in time.");
        }
    }

    internal static string StripRpcPrefix(string message)
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

        public static async Task<Helper> CreateAsync(McpConnectorHub hub)
        {
            if (!hub.IsCliInstalled)
                throw new InvalidOperationException("The Copilot CLI is not installed. Open the AI chat to install it.");

            var connection = RuntimeConnection.ForStdio(CopilotModule.GetCliPath(hub._paths), []);
            connection.Environment = await GetRuntimeEnvironmentAsync();

            // No Copilot account is needed to start MCP servers.
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
