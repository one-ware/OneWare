using Microsoft.Extensions.DependencyInjection;
using OneWare.Acp.Services;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Acp;

public class AcpModule : OneWareModuleBase
{
    private const string CodexAcpVersion = "0.16.0";
    private const string CodexAcpTag = "v0.16.0";
    private const string BaseUrl = $"https://github.com/zed-industries/codex-acp/releases/download/{CodexAcpTag}";

    public static readonly Package CodexPackage = new()
    {
        Category = "Binaries",
        Id = "codexacp",
        Type = "NativeTool",
        Name = "Codex ACP",
        Description = "ACP adapter for OpenAI Codex CLI, enabling Codex in ACP-compatible editors (by Zed Industries)",
        License = "Apache-2.0",
        IconUrl = "https://raw.githubusercontent.com/lobehub/lobe-icons/refs/heads/master/packages/static-png/dark/openai.png",
        Links =
        [
            new PackageLink { Name = "GitHub", Url = "https://github.com/zed-industries/codex-acp" },
            new PackageLink { Name = "OpenAI Codex", Url = "https://github.com/openai/codex" }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = CodexAcpVersion,
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = $"{BaseUrl}/codex-acp-{CodexAcpVersion}-x86_64-pc-windows-msvc.zip",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "codex-acp.exe", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "win-arm64",
                        Url = $"{BaseUrl}/codex-acp-{CodexAcpVersion}-aarch64-pc-windows-msvc.zip",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "codex-acp.exe", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url = $"{BaseUrl}/codex-acp-{CodexAcpVersion}-x86_64-unknown-linux-musl.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "codex-acp", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "linux-arm64",
                        Url = $"{BaseUrl}/codex-acp-{CodexAcpVersion}-aarch64-unknown-linux-musl.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "codex-acp", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url = $"{BaseUrl}/codex-acp-{CodexAcpVersion}-x86_64-apple-darwin.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "codex-acp", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url = $"{BaseUrl}/codex-acp-{CodexAcpVersion}-aarch64-apple-darwin.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "codex-acp", SettingKey = CodexChatService.CodexPathKey }]
                    }
                ]
            }
        ]
    };

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<CodexChatService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IPackageService>().RegisterPackage(CodexPackage);

        var settings = serviceProvider.Resolve<ISettingsService>();

        settings.RegisterSetting(
            "AI Chat", "Codex", CodexChatService.CodexPathKey,
            new FilePathSetting(
                "Codex ACP Path", "", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory,
                PlatformHelper.Exists,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path to the codex-acp binary. Install it automatically via the Package Manager."
            });

        settings.RegisterSetting(
            "AI Chat", "Codex", CodexChatService.CodexApiKeySettingKey,
            new TextBoxSetting("OpenAI API Key", "", null)
            {
                HoverDescription = "Your OpenAI API key (sk-...). Required to authenticate Codex with the OpenAI API."
            });

        var codex = serviceProvider.Resolve<CodexChatService>();
        serviceProvider.Resolve<IChatManagerService>().RegisterChatService(codex);

        _ = codex.CheckForUpdateAsync();
    }
}
