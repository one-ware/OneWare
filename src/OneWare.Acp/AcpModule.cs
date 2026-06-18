using Microsoft.Extensions.DependencyInjection;
using OneWare.Acp.Services;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.Acp;

public class AcpModule : OneWareModuleBase
{
    private const string CodexVersion = "0.141.0";
    private const string CodexTag = "rust-v0.141.0";

    public static readonly Package CodexPackage = new()
    {
        Category = "Binaries",
        Id = "codexcli",
        Type = "NativeTool",
        Name = "Codex CLI",
        Description = "OpenAI Codex agent with ACP support for OneWare Studio",
        License = "Apache-2.0",
        IconUrl = "https://raw.githubusercontent.com/lobehub/lobe-icons/refs/heads/master/packages/static-png/dark/openai.png",
        Links =
        [
            new PackageLink { Name = "GitHub", Url = "https://github.com/openai/codex" },
            new PackageLink { Name = "Documentation", Url = "https://github.com/openai/codex#readme" }
        ],
        Versions =
        [
            new PackageVersion
            {
                Version = CodexVersion,
                Targets =
                [
                    new PackageTarget
                    {
                        Target = "win-x64",
                        Url = $"https://github.com/openai/codex/releases/download/{CodexTag}/codex-package-x86_64-pc-windows-msvc.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "codex.exe", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "win-arm64",
                        Url = $"https://github.com/openai/codex/releases/download/{CodexTag}/codex-package-aarch64-pc-windows-msvc.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "bin/codex.exe", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "linux-x64",
                        Url = $"https://github.com/openai/codex/releases/download/{CodexTag}/codex-package-x86_64-unknown-linux-musl.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "bin/codex", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "linux-arm64",
                        Url = $"https://github.com/openai/codex/releases/download/{CodexTag}/codex-package-aarch64-unknown-linux-musl.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "bin/codex", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "osx-x64",
                        Url = $"https://github.com/openai/codex/releases/download/{CodexTag}/codex-package-x86_64-apple-darwin.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "bin/codex", SettingKey = CodexChatService.CodexPathKey }]
                    },
                    new PackageTarget
                    {
                        Target = "osx-arm64",
                        Url = $"https://github.com/openai/codex/releases/download/{CodexTag}/codex-package-aarch64-apple-darwin.tar.gz",
                        AutoSetting = [new PackageAutoSetting { RelativePath = "bin/codex", SettingKey = CodexChatService.CodexPathKey }]
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

        serviceProvider.Resolve<ISettingsService>().RegisterSetting(
            "AI Chat", "Codex CLI", CodexChatService.CodexPathKey,
            new FilePathSetting(
                "Codex CLI Path", "", null,
                serviceProvider.Resolve<IPaths>().NativeToolsDirectory,
                PlatformHelper.Exists,
                PlatformHelper.ExeFile)
            {
                HoverDescription = "Path to the Codex CLI binary. Install it automatically via the Package Manager."
            });

        var codex = serviceProvider.Resolve<CodexChatService>();

        serviceProvider.Resolve<IChatManagerService>().RegisterChatService(codex);

        // Kick off update check in the background — shows an update button in chat if needed
        _ = codex.CheckForUpdateAsync();
    }
}
