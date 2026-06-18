using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Acp.Services;

/// <summary>
/// ACP chat service backed by the OpenAI Codex CLI.
/// The binary is managed by the OneWare package manager; no user settings are required.
/// </summary>
public sealed class CodexChatService(
    IPaths paths,
    ISettingsService settingsService,
    IPackageService packageService,
    IPackageWindowService packageWindowService,
    ILogger logger)
    : AcpChatService(paths, logger)
{
    // Silently-stored path key — populated automatically by PackageAutoSetting after install.
    // Not registered via ISettingsService.RegisterSetting so it never appears in the Settings UI.
    internal const string CodexPathKey = "AI_Chat_Codex_Path";

    private static readonly string CodexExe =
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows)
            ? "codex.exe"
            : "codex";

    public override string Name => "Codex";

    // Tell Codex to start in ACP server mode over stdin/stdout instead of interactive TUI mode.
    protected override IEnumerable<string> GetAgentArguments() => ["-- --acp"];

    protected override string? ResolveAgentPath()
    {
        // 1. Path written by the package manager after install
        var fromPackage = settingsService.GetSettingValue<string>(CodexPathKey);
        if (PlatformHelper.Exists(fromPackage))
            return PlatformHelper.GetFullPath(fromPackage);

        // 2. Codex on the system PATH (e.g. `npm install -g @openai/codex`)
        return PlatformHelper.GetFullPath(CodexExe);
    }

    protected override void OnAgentNotFound()
    {
        RaiseStatusChanged(new StatusEvent(false, "Codex not found"));
        RaiseEventReceived(new ChatButtonEvent(
            "Codex CLI is not installed. Download it via the Package Manager.",
            "Install Codex CLI",
            new AsyncRelayCommand<Control?>(owner => InstallCodexAsync(owner))));
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
            RaiseStatusChanged(new StatusEvent(false, "Codex update available"));
            RaiseEventReceived(new ChatButtonEvent(
                "A Codex CLI update is available.",
                "Update Codex CLI",
                new AsyncRelayCommand<Control?>(owner => InstallCodexAsync(owner, update: true))));
        }
    }
}
