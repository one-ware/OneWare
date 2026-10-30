using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using GitHub.Copilot;
using Microsoft.Extensions.Logging.Abstractions;
using OneWare.Copilot.Connectors;
using OneWare.Essentials.Services;
using Xunit;

namespace OneWare.Copilot.UnitTests;

public sealed class ConnectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OneWare-Connectors-" + Guid.NewGuid());
    private readonly List<string> _warnings = [];
    private readonly FakeSecretStore _secrets = new();
    private readonly List<McpConnectorHub> _hubs = [];

    public ConnectorTests()
    {
        Directory.CreateDirectory(_root);
    }

    private IReadOnlyList<ImportedMcpConnector> Import(string json)
    {
        var file = Path.Combine(_root, ".mcp.json");
        File.WriteAllText(file, json);
        return McpConnectorImport.Read(file, _warnings.Add);
    }

    private McpConnectorHub CreateHub()
    {
        var hub = new McpConnectorHub(_secrets, new FakePaths(Path.Combine(_root, "appdata")), NullLogger.Instance);
        _hubs.Add(hub);
        return hub;
    }

    private static JsonObject Json(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void FromConfig_ReadsCliConfigurationsAndKeepsUnknownFields()
    {
        // The CLI omits "type" for stdio servers.
        var local = McpConnector.FromConfig("mail", Json("""
            {"command":"npx","args":["-y","ms365"],"env":{"TOKEN":"${MS_TOKEN}"},"cwd":"/work","tools":["read"],
             "timeout":5000,"disabled":true}
            """));

        Assert.Equal(McpConnectorKind.Local, local.Kind);
        Assert.False(local.Enabled);
        Assert.Equal("npx -y ms365", local.Describe());
        Assert.Equal([new McpConnectorValue("TOKEN", "${MS_TOKEN}", false)], local.Env);

        var config = local.ToCliConfig();
        Assert.Equal("stdio", config["type"]!.GetValue<string>());
        Assert.Equal("/work", config["cwd"]!.GetValue<string>());
        Assert.Equal(5000, config["timeout"]!.GetValue<int>());
        Assert.Equal("read", config["tools"]![0]!.GetValue<string>());
        Assert.Null(config["disabled"]);

        var remote = McpConnector.FromConfig("asana", Json("""
            {"type":"sse","url":"https://mcp.asana.com/sse","headers":{"X":"1"},"oauthClientId":"abc","oauthPublicClient":false}
            """));

        Assert.Equal(McpConnectorKind.Remote, remote.Kind);
        Assert.True(remote.UseSse);
        Assert.False(remote.OAuthPublicClient);
        var remoteConfig = remote.ToCliConfig();
        Assert.Equal("sse", remoteConfig["type"]!.GetValue<string>());
        Assert.Equal("abc", remoteConfig["oauthClientId"]!.GetValue<string>());
        Assert.Null(remoteConfig["command"]);
        Assert.Equal("*", remoteConfig["tools"]![0]!.GetValue<string>());
    }

    [Fact]
    public void StoredConfig_LeavesSecretValuesOutAndRoundTrips()
    {
        var connector = new McpConnector
        {
            Name = "gh",
            Kind = McpConnectorKind.Remote,
            Url = "https://api.githubcopilot.com/mcp/",
            Enabled = false,
            Headers =
            [
                new McpConnectorValue("Authorization", "Bearer s3cret", true),
                new McpConnectorValue("X-Mode", "${MODE}", false)
            ]
        };

        var stored = connector.ToStoredConfig();
        Assert.DoesNotContain("s3cret", stored.ToJsonString());
        Assert.Equal(connector.Id, stored[McpConnector.IdField]!.GetValue<string>());
        Assert.True(stored[McpConnector.DisabledField]!.GetValue<bool>());
        Assert.Null(stored["tools"]);

        var loaded = McpConnector.FromConfig("gh", stored);
        Assert.Equal(connector.Id, loaded.Id);
        Assert.False(loaded.Enabled);
        Assert.Equal(new McpConnectorValue("Authorization", "", true), loaded.Headers.Single(x => x.IsSecret));
        Assert.False(loaded.ToCliConfig(includeSecrets: false).ContainsKey("tools"));

        var secrets = connector.GetSecrets("client-secret");
        Assert.Equal("Bearer s3cret", secrets.Headers["Authorization"]);
        Assert.Equal("client-secret", secrets.OAuthClientSecret);

        var resolved = loaded.WithSecrets(secrets);
        var config = Assert.IsType<McpHttpServerConfig>(resolved.ToServerConfig());
        Assert.Equal("Bearer s3cret", config.Headers!["Authorization"]);
        Assert.Equal("${MODE}", config.Headers["X-Mode"]);
        // Secret values never count as ${VAR} references.
        Assert.Equal(["MODE"], resolved.GetVariables());

        resolved.UseSse = true;
        Assert.Null(resolved.ToServerConfig());
        Assert.Equal("sse", resolved.ToCliConfig()["type"]!.GetValue<string>());
    }

    [Fact]
    public void GetVariables_ListsReferencesOfTheActiveKind()
    {
        var connector = new McpConnector
        {
            Kind = McpConnectorKind.Local,
            Command = "${TOOLS}/server",
            Args = ["--token", "${B_TOKEN}", "${A}"],
            Env = [new McpConnectorValue("KEY", "prefix-${B_TOKEN}", false)],
            Headers = [new McpConnectorValue("Authorization", "${HEADER_ONLY}", false)]
        };

        Assert.Equal(["A", "B_TOKEN", "TOOLS"], connector.GetVariables());

        connector.Kind = McpConnectorKind.Remote;
        Assert.Equal(["HEADER_ONLY"], connector.GetVariables());
    }

    [Fact]
    public void Names_AndSecretKeys_AreRecognized()
    {
        Assert.True(McpConnector.IsValidName("ms365.mail_v2-x"));
        Assert.False(McpConnector.IsValidName("has space"));
        Assert.False(McpConnector.IsValidName("-._"));

        Assert.True(McpConnector.LooksSecret("Authorization"));
        Assert.True(McpConnector.LooksSecret("GITHUB_PERSONAL_ACCESS_TOKEN"));
        Assert.True(McpConnector.LooksSecret("x-api-key"));
        Assert.False(McpConnector.LooksSecret("LOG_LEVEL"));
    }

    [Fact]
    public void Import_KeepsVariableReferencesAndMarksCredentialsSecret()
    {
        var imported = Import("""
            {
              // comments are allowed
              "mcpServers": {
                "github": {
                  "type": "http",
                  "url": "https://api.githubcopilot.com/mcp/",
                  "headers": { "Authorization": "Bearer abc", "X-Ref": "${GH_TOKEN}" }
                },
                "mail": {
                  "command": "npx",
                  "args": ["-y", "ms365", "${workspaceFolder}/data", "${HOME_DIR:-/tmp}"],
                  "env": { "TOKEN": "${MS_TOKEN}" },
                  "disabled": true
                },
                "asana": {
                  "url": "https://mcp.asana.com/v2/mcp",
                  "oauthClientId": "client",
                  "oauthClientSecret": "secret"
                },
              }
            }
            """);

        Assert.Equal(["github", "mail", "asana"], imported.Select(x => x.Connector.Name));

        var github = imported[0].Connector;
        Assert.True(github.Headers.Single(x => x.Key == "Authorization").IsSecret);
        // References are resolved by the runtime and stay in mcp.json.
        Assert.False(github.Headers.Single(x => x.Key == "X-Ref").IsSecret);

        var mail = imported[1].Connector;
        Assert.False(mail.Enabled);
        Assert.Equal(["-y", "ms365", $"{_root}/data", "${HOME_DIR}"], mail.Args);
        Assert.False(mail.Env.Single().IsSecret);
        // A project server ran in its folder.
        Assert.Equal(_root, mail.WorkingDirectory);
        Assert.Contains(_warnings, x => x.Contains("HOME_DIR"));

        var asana = imported[2];
        Assert.Equal(McpConnectorKind.Remote, asana.Connector.Kind);
        Assert.Equal("client", asana.Connector.OAuthClientId);
        Assert.Equal("secret", asana.OAuthClientSecret);
        Assert.False(asana.Connector.ToCliConfig().ContainsKey("oauthClientSecret"));
    }

    [Fact]
    public void Import_AcceptsVsCodeAndClaudeFormatsAndReportsInvalidServers()
    {
        var vsCode = Import("""{"servers":{"a":{"type":"stdio","command":"a","cwd":"sub"}}}""");
        Assert.Equal(Path.Combine(_root, "sub"), vsCode.Single().Connector.WorkingDirectory);

        var claude = Import("""
            {"b":{"type":"streamable-http","url":"https://b.dev"},"c":{"type":"websocket","url":"ws://c"},
             "bad name":{"command":"x"},"d":{"type":"stdio"}}
            """);
        Assert.Equal("b", claude.Single().Connector.Name);
        Assert.Contains(_warnings, x => x.Contains("websocket"));
        Assert.Contains(_warnings, x => x.Contains("bad name"));
        Assert.Contains(_warnings, x => x.StartsWith("d:"));

        Assert.Empty(Import("{ not json"));
        Assert.Contains(_warnings, x => x.Contains("not valid JSON"));
    }

    [Theory]
    [InlineData("""{"command":"npx","args":["-y","pkg"]}""", "fallback")]
    [InlineData("""
        "github": {"command":"npx","args":["-y","pkg"]},
        """, "github")]
    [InlineData("""{"mcpServers":{"github":{"command":"npx","args":["-y","pkg"]}}}""", "github")]
    [InlineData("""{"servers":{"github":{"command":"npx","args":["-y","pkg"]}}}""", "github")]
    public void ParseText_AcceptsPastedSnippets(string text, string expectedName)
    {
        var connector = McpConnectorImport.ParseText(text, _warnings.Add, "fallback").Single().Connector;

        Assert.Equal(expectedName, connector.Name);
        Assert.Equal("npx", connector.Command);
        Assert.Equal(["-y", "pkg"], connector.Args);
        Assert.Null(connector.WorkingDirectory);
    }

    [Fact]
    public void ParseText_KeepsWorkspaceFolderAndWarns()
    {
        var connector = McpConnectorImport.ParseText("""{"command":"srv","args":["${workspaceFolder}"]}""",
            _warnings.Add).Single().Connector;

        Assert.Equal(["${workspaceFolder}"], connector.Args);
        Assert.Contains(_warnings, x => x.Contains("workspaceFolder"));
        Assert.Empty(McpConnectorImport.ParseText("[]", _warnings.Add));
    }

    [Fact]
    public void RuntimeEnvironment_LayersLoginShellAndProcess()
    {
        var sep = Path.PathSeparator;
        var loginShell = new Dictionary<string, string>
        {
            ["PATH"] = $"/home/u/.nvm/bin{sep}/usr/bin",
            ["TOKEN"] = "from-shell",
            ["SHELL_ONLY"] = "1"
        };
        var process = new Dictionary<string, string>
        {
            ["PATH"] = $"/usr/bin{sep}/app/bin",
            ["TOKEN"] = "from-process"
        };
        var env = CopilotRuntimeEnvironment.Merge(process, loginShell, ignoreCase: false);

        Assert.Equal($"/home/u/.nvm/bin{sep}/usr/bin{sep}/app/bin", env["PATH"]);
        Assert.Equal("from-process", env["TOKEN"]);
        Assert.Equal("1", env["SHELL_ONLY"]);

        var withoutShell = CopilotRuntimeEnvironment.Merge(process, new Dictionary<string, string>(),
            ignoreCase: false);
        Assert.Equal(process["PATH"], withoutShell["PATH"]);
        Assert.Equal("from-process", withoutShell["TOKEN"]);
    }

    [Fact]
    public void SignInErrors_ExplainMissingAppRegistration()
    {
        var message = McpConnectorHub.DescribeSignInError(
            "Client registration failed: Authorization server does not support dynamic client registration");

        Assert.Contains(McpConnectorHub.OAuthRedirectUrl, message);
        Assert.True(McpConnectorHub.IsDynamicRegistrationError(message));
        Assert.Equal("other", McpConnectorHub.DescribeSignInError("other"));
        Assert.False(McpConnectorHub.IsDynamicRegistrationError("other"));
    }

    [Fact]
    public async Task Hub_SavesSecretsInTheStoreAndRenamesInPlace()
    {
        var hub = CreateHub();
        Assert.Empty(await hub.GetConnectorsAsync());

        var connector = new McpConnector
        {
            Name = "github",
            Kind = McpConnectorKind.Local,
            Command = "npx",
            Env = [new McpConnectorValue("GITHUB_TOKEN", "ghp_123", true)]
        };
        await hub.SaveAsync(connector, null);
        await hub.SaveAsync(new McpConnector { Name = "zeta", Command = "z" }, null);

        var file = await File.ReadAllTextAsync(hub.ConfigFile);
        Assert.DoesNotContain("ghp_123", file);
        Assert.Contains("ghp_123", _secrets.Get(McpConnectorHub.SecretService, connector.Id));

        // A second hub reads the file from disk.
        var other = CreateHub();
        var loaded = (await other.GetConnectorsAsync()).First();
        Assert.Equal("github", loaded.Name);
        Assert.Equal("ghp_123", (await other.ResolveAsync(loaded)).Env.Single().Value);

        var renamed = (await other.ResolveAsync(loaded)).Clone();
        renamed.Name = "gh";
        await other.SaveAsync(renamed, null);
        Assert.Equal(["gh", "zeta"], (await other.GetConnectorsAsync()).Select(x => x.Name));
        // The file keeps its order.
        var keys = JsonNode.Parse(await File.ReadAllTextAsync(hub.ConfigFile))!["mcpServers"]!.AsObject()
            .Select(x => x.Key);
        Assert.Equal(["gh", "zeta"], keys);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            other.SaveAsync(new McpConnector { Name = "GH", Command = "x" }, null));

        var resolved = Assert.Single(await other.ResolveEnabledAsync(), x => x.Name == "gh");
        Assert.Equal("ghp_123", resolved.Config["env"]!["GITHUB_TOKEN"]!.GetValue<string>());
        var fingerprint = resolved.Fingerprint;

        await other.SetEnabledAsync(loaded.Id, false);
        Assert.DoesNotContain(await other.ResolveEnabledAsync(), x => x.Name == "gh");
        await other.SetEnabledAsync(loaded.Id, true);
        Assert.Equal(fingerprint, (await other.ResolveEnabledAsync()).Single(x => x.Name == "gh").Fingerprint);

        await other.RemoveAsync(loaded.Id);
        Assert.Null(_secrets.Get(McpConnectorHub.SecretService, loaded.Id));
        Assert.Equal(["zeta"], (await other.GetConnectorsAsync()).Select(x => x.Name));
    }

    [Fact]
    public async Task Hub_AssignsIdsToHandWrittenEntriesAndReportsErrors()
    {
        var hub = CreateHub();
        Directory.CreateDirectory(Path.GetDirectoryName(hub.ConfigFile)!);
        await File.WriteAllTextAsync(hub.ConfigFile, """
            {
              "mcpServers": {
                "a": { "command": "a" },
                "bad name": { "command": "b" }
              }
            }
            """);

        var connector = Assert.Single(await hub.GetConnectorsAsync());
        Assert.False(string.IsNullOrEmpty(connector.Id));
        Assert.Contains(connector.Id, await File.ReadAllTextAsync(hub.ConfigFile));

        var added = await hub.AddAsync(McpConnectorImport.ParseText("""
            {"mcpServers":{"A":{"command":"dup"},"new":{"url":"https://x.dev/mcp","headers":{"Authorization":"Bearer t"}}}}
            """, _warnings.Add), _warnings.Add);
        Assert.Equal(["new"], added);
        Assert.Contains(_warnings, x => x.Contains("'A' already exists"));

        var broken = CreateHub();
        await File.WriteAllTextAsync(broken.ConfigFile, "{ broken");
        Assert.Empty(await broken.GetConnectorsAsync());
        Assert.NotNull(broken.LoadError);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            broken.SaveAsync(new McpConnector { Name = "x", Command = "x" }, null));
        Assert.Equal("{ broken", await File.ReadAllTextAsync(broken.ConfigFile));
    }

    public void Dispose()
    {
        foreach (var hub in _hubs) hub.Dispose();
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        private readonly Dictionary<(string, string), string> _values = new();

        public string? Get(string service, string account) => _values.GetValueOrDefault((service, account));

        public void Set(string service, string account, string secret) => _values[(service, account)] = secret;

        public void Remove(string service, string account) => _values.Remove((service, account));
    }

    private sealed class FakePaths(string root) : IPaths
    {
        public string AppName => "OneWare";
        public string AppIconPath => string.Empty;
        public string AppFolderName => "OneWare";
        public string AppDataDirectory => root;
        public string TempDirectory => Path.Combine(root, "temp");
        public string SessionDirectory => Path.Combine(root, "session");
        public string LayoutDirectory => Path.Combine(root, "layout");
        public string SettingsPath => Path.Combine(root, "settings.json");
        public string DocumentsDirectory => root;
        public string CrashReportsDirectory => root;
        public string ProjectsDirectory => root;
        public string PackagesDirectory => root;
        public string NativeToolsDirectory => root;
        public string OnnxRuntimesDirectory => root;
        public string PluginsDirectory => root;
        public string ChangelogUrl => string.Empty;
        public string UpdateInfoUrl => string.Empty;
    }
}
