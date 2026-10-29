using GitHub.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using OneWare.Copilot.Connectors;
using Xunit;

namespace OneWare.Copilot.UnitTests;

public sealed class ConnectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OneWare-Connectors-" + Guid.NewGuid());
    private readonly List<string> _warnings = [];

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

    [Fact]
    public void FromConfig_ReadsCliConfigurationsAndKeepsUnknownFields()
    {
        // The CLI omits "type" for stdio servers when it stores them.
        var local = McpConnector.FromConfig("mail", JsonNode.Parse("""
            {"command":"npx","args":["-y","ms365"],"env":{"TOKEN":"${MS_TOKEN}"},"cwd":"/work","tools":["read"],"timeout":5000}
            """)!.AsObject(), enabled: false);

        Assert.Equal(McpConnectorKind.Local, local.Kind);
        Assert.False(local.Enabled);
        Assert.Equal("npx -y ms365", local.Describe());
        Assert.Equal([new McpConnectorValue("TOKEN", "${MS_TOKEN}")], local.Env);

        var config = local.ToConfig();
        Assert.Equal("stdio", config["type"]!.GetValue<string>());
        Assert.Equal("/work", config["cwd"]!.GetValue<string>());
        Assert.Equal(5000, config["timeout"]!.GetValue<int>());
        Assert.Equal("read", config["tools"]![0]!.GetValue<string>());

        var remote = McpConnector.FromConfig("asana", JsonNode.Parse("""
            {"type":"sse","url":"https://mcp.asana.com/sse","headers":{"X":"1"},"oauthClientId":"abc","oauthPublicClient":false}
            """)!.AsObject(), enabled: true);

        Assert.Equal(McpConnectorKind.Remote, remote.Kind);
        Assert.True(remote.UseSse);
        Assert.False(remote.OAuthPublicClient);
        var remoteConfig = remote.ToConfig();
        Assert.Equal("sse", remoteConfig["type"]!.GetValue<string>());
        Assert.Equal("abc", remoteConfig["oauthClientId"]!.GetValue<string>());
        Assert.Null(remoteConfig["command"]);
        Assert.Equal("*", remoteConfig["tools"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Clone_DoesNotAddTheDefaultToolFilter()
    {
        var connector = McpConnector.FromConfig("x", JsonNode.Parse("""{"url":"https://x.dev/mcp"}""")!.AsObject(),
            true);

        Assert.False(connector.Clone().OtherFields.ContainsKey("tools"));
        Assert.Equal(McpConnectorKind.Remote, connector.Kind);
    }

    [Fact]
    public void GetVariables_ListsReferencesOfTheActiveKind()
    {
        var connector = new McpConnector
        {
            Kind = McpConnectorKind.Local,
            Command = "${TOOLS}/server",
            Args = ["--token", "${B_TOKEN}", "${A}"],
            Env = [new McpConnectorValue("KEY", "prefix-${B_TOKEN}")],
            Headers = [new McpConnectorValue("Authorization", "Bearer ${HEADER_ONLY}")]
        };

        Assert.Equal(["A", "B_TOKEN", "TOOLS"], connector.GetVariables());

        connector.Kind = McpConnectorKind.Remote;
        Assert.Equal(["HEADER_ONLY"], connector.GetVariables());
    }

    [Fact]
    public void SecretVariableNames_AreSanitizedAndRecognized()
    {
        Assert.Equal("ONEWARE_MCP_MY_SERVER__X_API_KEY",
            McpConnector.GetSecretVariableName("my-server", "x-api-key"));
        Assert.True(McpConnector.IsVariableReference("${ONEWARE_MCP_A__B}", out var name));
        Assert.Equal("ONEWARE_MCP_A__B", name);
        Assert.False(McpConnector.IsVariableReference("Bearer ${TOKEN}", out _));
        Assert.True(McpConnector.IsValidName("ms365.mail_v2-x"));
        Assert.False(McpConnector.IsValidName("has space"));
        Assert.False(McpConnector.IsValidName("-._"));
    }

    [Fact]
    public void SecretVariableNames_DontCollideAcrossConnectors()
    {
        // Splitting differently between connector name and key gives different variables.
        Assert.NotEqual(McpConnector.GetSecretVariableName("a", "B_TOKEN"),
            McpConnector.GetSecretVariableName("a_b", "TOKEN"));
        Assert.NotEqual(McpConnector.GetSecretVariableName("a", "b__c"),
            McpConnector.GetSecretVariableName("a__b", "c"));
        Assert.StartsWith(McpConnector.GetSecretVariablePrefix("a"), McpConnector.GetSecretVariableName("a", "x"));
        Assert.False(McpConnector.GetSecretVariableName("a_b", "x")
            .StartsWith(McpConnector.GetSecretVariablePrefix("a"), StringComparison.Ordinal));

        // Names that only differ in case or punctuation share a key and are rejected as duplicates.
        Assert.Equal(McpConnector.GetVariableKey("my-server"), McpConnector.GetVariableKey("My__Server"));
    }

    [Fact]
    public void WithVariables_FillsOnlyKnownReferences()
    {
        var connector = new McpConnector
        {
            Name = "x",
            Kind = McpConnectorKind.Local,
            Command = "${BIN}/srv",
            Args = ["--token", "${SECRET}", "${OTHER}"],
            Env = [new McpConnectorValue("KEY", "Bearer ${SECRET}")]
        };
        var values = new Dictionary<string, string> { ["SECRET"] = "s3", ["BIN"] = "/opt" };

        var resolved = connector.WithVariables(values);

        Assert.Equal("/opt/srv", resolved.Command);
        Assert.Equal(["--token", "s3", "${OTHER}"], resolved.Args);
        Assert.Equal("Bearer s3", resolved.Env[0].Value);
        Assert.EndsWith("${SECRET}", connector.Env[0].Value);

        var config = Assert.IsType<McpStdioServerConfig>(resolved.ToServerConfig());
        Assert.Equal("/opt/srv", config.Command);
        Assert.Equal("Bearer s3", config.Env!["KEY"]);

        var remote = new McpConnector
        {
            Name = "y",
            Kind = McpConnectorKind.Remote,
            Url = "https://example.com/mcp",
            Headers = [new McpConnectorValue("Authorization", "Bearer ${SECRET}")]
        }.WithVariables(values);
        var remoteConfig = Assert.IsType<McpHttpServerConfig>(remote.ToServerConfig());
        Assert.Equal("Bearer s3", remoteConfig.Headers!["Authorization"]);
        remote.UseSse = true;
        Assert.Null(remote.ToServerConfig());
    }

    [Fact]
    public void Import_KeepsVariableReferencesForTheCli()
    {
        var imported = Import("""
            {
              // comments are allowed
              "mcpServers": {
                "github": {
                  "type": "http",
                  "url": "https://api.githubcopilot.com/mcp/",
                  "headers": { "Authorization": "Bearer ${env:GITHUB_PERSONAL_ACCESS_TOKEN}" }
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
        Assert.Equal("Bearer ${GITHUB_PERSONAL_ACCESS_TOKEN}", github.Headers.Single().Value);

        var mail = imported[1].Connector;
        Assert.False(mail.Enabled);
        Assert.Equal(["-y", "ms365", $"{_root}/data", "${HOME_DIR}"], mail.Args);
        Assert.Equal("${MS_TOKEN}", mail.Env.Single().Value);
        // A project server ran in its folder.
        Assert.Equal(_root, mail.WorkingDirectory);
        Assert.Contains(_warnings, x => x.Contains("HOME_DIR"));

        var asana = imported[2];
        Assert.Equal(McpConnectorKind.Remote, asana.Connector.Kind);
        Assert.Equal("client", asana.Connector.OAuthClientId);
        Assert.Equal("secret", asana.OAuthClientSecret);
        Assert.False(asana.Connector.ToConfig().ContainsKey("oauthClientSecret"));
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
        Assert.Contains(_warnings, x => x.StartsWith("Failed to read"));
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
        var message = CopilotConnectorService.DescribeSignInError(
            "Client registration failed: Authorization server does not support dynamic client registration");

        Assert.Contains(CopilotConnectorService.OAuthRedirectUrl, message);
        Assert.Equal("other", CopilotConnectorService.DescribeSignInError("other"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }
}
