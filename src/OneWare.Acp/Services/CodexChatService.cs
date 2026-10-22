using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using dotacp.protocol.unstable;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Acp.Services;

/// <summary>
/// ACP chat service backed by the zed-industries/codex-acp binary (OpenAI Codex via ACP).
/// </summary>
public sealed class CodexChatService(
    IPaths paths,
    ISettingsService settingsService,
    IPackageService packageService,
    IPackageWindowService packageWindowService,
    ITerminalManagerService terminalManagerService,
    ILogger logger)
    : AcpChatService(paths, logger)
{
    // Silently-stored path key — populated automatically by PackageAutoSetting after install.
    internal const string CodexPathKey = "AI_Chat_Codex_Path";

    /// <summary>
    /// User-visible setting key for the OpenAI API key.
    /// Registered in <see cref="AcpModule"/> under "AI Chat → Codex".
    /// </summary>
    internal const string CodexApiKeySettingKey = "AI_Chat_Codex_ApiKey";

    private static readonly string CodexExe =
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows)
            ? "codex-acp.exe"
            : "codex-acp";

    public override string Name => "Codex";

    // codex-acp starts in ACP server mode over stdin/stdout by default — no extra flags needed.

    protected override string? ResolveAgentPath()
    {
        // 1. Path written by the package manager after install
        var fromPackage = settingsService.GetSettingValue<string>(CodexPathKey);
        if (PlatformHelper.Exists(fromPackage))
            return PlatformHelper.GetFullPath(fromPackage);

        // 2. Codex on the system PATH
        return PlatformHelper.GetFullPath(CodexExe);
    }

    protected override IReadOnlyDictionary<string, string> GetEnvironmentVariables()
    {
        var dict = new Dictionary<string, string>();
        var apiKey = settingsService.GetSettingValue<string>(CodexApiKeySettingKey);
        if (!string.IsNullOrWhiteSpace(apiKey))
            dict["OPENAI_API_KEY"] = apiKey;
        return dict;
    }

    protected override void OnAgentNotFound()
    {
        RaiseStatusChanged(new StatusEvent(false, "Codex ACP not found"));
        RaiseEventReceived(new ChatButtonEvent(
            "codex-acp is not installed. Download it via the Package Manager.",
            "Install Codex ACP",
            new AsyncRelayCommand<Control?>(owner => InstallCodexAsync(owner))));
    }

    protected override void OnAuthRequired(AuthMethodEnvVar method, IReadOnlyList<AuthEnvVar> missingVars)
    {
        var link = method.Link;
        var linkText = link != null ? $"\n\nGet your key at: {link}" : string.Empty;
        RaiseStatusChanged(new StatusEvent(false, "OpenAI API key required"));
        RaiseEventReceived(new ChatErrorEvent(
            $"An OpenAI API key is required to use Codex. " +
            $"Enter it in **Settings → AI Chat → Codex → OpenAI API Key**.{linkText}"));
    }

    protected override void OnTerminalAuthRequired(AuthMethodTerminal method)
    {
        RaiseStatusChanged(new StatusEvent(false, "Login required"));
        RaiseEventReceived(new ChatButtonEvent(
            $"Login with your ChatGPT account to use Codex. " +
            "A browser window will open to complete the sign-in.",
            "Login with ChatGPT",
            new AsyncRelayCommand<Control?>(async _ =>
            {
                await RunTerminalAuthAsync(method).ConfigureAwait(false);
                await InitializeAsync().ConfigureAwait(false);
            })));
    }

    protected override void OnAgentAuthRequired(AuthMethodAgent method)
    {
        var description = string.IsNullOrWhiteSpace(method.Description)
            ? "Login with your ChatGPT account to use Codex. A browser window will open to complete the sign-in."
            : method.Description;
        var buttonLabel = string.IsNullOrWhiteSpace(method.Name) ? "Login with ChatGPT" : method.Name;

        RaiseStatusChanged(new StatusEvent(false, "Login required"));
        RaiseEventReceived(new ChatButtonEvent(
            description,
            buttonLabel,
            new AsyncRelayCommand<Control?>(async _ =>
            {
                RaiseStatusChanged(new StatusEvent(false, "Authenticating\u2026"));
                await PerformAgentAuthAndOpenSessionAsync(method.Id).ConfigureAwait(false);
            })));
    }

    // ── Install / update ──────────────────────────────────────────────────────

    private async Task InstallCodexAsync(Control? owner, bool update = false)
    {
        if (!update && ResolveAgentPath() != null) return;

        var installed = await packageWindowService
            .QuickInstallPackageAsync(AcpModule.CodexPackage.Id!)
            .ConfigureAwait(false);

        if (!installed) return;

        RaiseSessionReset();
        await InitializeAsync().ConfigureAwait(false);
    }

    // ── Update check ──────────────────────────────────────────────────────────

    internal async Task CheckForUpdateAsync()
    {
        if (!packageService.IsLoaded && !await packageService.RefreshAsync().ConfigureAwait(false))
            return;

        if (packageService.Packages.TryGetValue(AcpModule.CodexPackage.Id!, out var state) &&
            state.Status is PackageStatus.UpdateAvailable)
        {
            RaiseStatusChanged(new StatusEvent(false, "Codex ACP update available"));
            RaiseEventReceived(new ChatButtonEvent(
                "A Codex ACP update is available.",
                "Update Codex ACP",
                new AsyncRelayCommand<Control?>(owner => InstallCodexAsync(owner, update: true))));
        }
    }

    // ── Terminal auth ─────────────────────────────────────────────────────────

    private async Task RunTerminalAuthAsync(AuthMethodTerminal method)
    {
        var agentPath = ResolveAgentPath();
        if (agentPath == null)
        {
            RaiseEventReceived(new ChatErrorEvent("codex-acp binary not found. Please install it first."));
            return;
        }

        // Build command: <binary> [extra args from AuthMethodTerminal]
        var extraArgs = method.Args ?? [];
        var argString = extraArgs.Length > 0 ? " " + string.Join(" ", extraArgs) : string.Empty;
        var command = $"\"{agentPath}\"{argString}";

        RaiseStatusChanged(new StatusEvent(false, "Waiting for login…"));

        var result = await terminalManagerService.ExecuteInTerminalAsync(
            command,
            id: "codex-auth",
            showInUi: true,
            timeout: TimeSpan.FromMinutes(10)).ConfigureAwait(false);

        if (result.TimedOut)
        {
            RaiseEventReceived(new ChatErrorEvent("Login timed out. Please try again."));
        }
        else if (result.ExitCode != 0)
        {
            RaiseEventReceived(new ChatErrorEvent(
                $"Login process exited with code {result.ExitCode}. Please try again."));
        }
    }
}
