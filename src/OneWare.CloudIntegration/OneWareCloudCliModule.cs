using System.CommandLine;
using System.IO.Pipes;
using Microsoft.Extensions.DependencyInjection;
using OneWare.CloudIntegration.Services;
using OneWare.Essentials.Services;

namespace OneWare.CloudIntegration;

public sealed class OneWareCloudCliModule : OneWareCliModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<OneWareCloudLoginService>();
    }

    public override IReadOnlyList<Command> RegisterCommands(IServiceProvider serviceProvider)
    {
        EnsureSettingsInitialized(serviceProvider);

        var cloudCommand = new Command("cloud", "OneWare Cloud commands");

        var loginCommand = new Command("login", "Log in to OneWare Cloud");
        loginCommand.SetAction((_, cancellationToken) => LoginAsync(serviceProvider, cancellationToken));

        var logoutCommand = new Command("logout", "Log out of OneWare Cloud");
        logoutCommand.SetAction((_, cancellationToken) => LogoutAsync(serviceProvider, cancellationToken));

        cloudCommand.Subcommands.Add(loginCommand);
        cloudCommand.Subcommands.Add(logoutCommand);
        return [cloudCommand];
    }

    private static async Task<int> LoginAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        var loginService = serviceProvider.GetRequiredService<OneWareCloudLoginService>();

        Console.WriteLine("Opening browser for OneWare Cloud login...");

        var success = await loginService.LoginAsync(cancellationToken);
        if (!success)
        {
            Console.Error.WriteLine("OneWare Cloud login failed.");
            return 1;
        }

        var userId = settingsService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountUserIdKey);
        if (string.IsNullOrWhiteSpace(userId))
        {
            Console.Error.WriteLine("OneWare Cloud login did not produce a stored account.");
            return 1;
        }

        Console.WriteLine("Logged in to OneWare Cloud.");
        return 0;
    }

    private static async Task<int> LogoutAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        var paths = serviceProvider.GetRequiredService<IPaths>();
        var userId = settingsService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountUserIdKey);

        if (string.IsNullOrWhiteSpace(userId))
        {
            Console.WriteLine("OneWare Cloud is already logged out.");
            return 0;
        }

        try
        {
            serviceProvider.GetRequiredService<OneWareCloudLoginService>().Logout(userId);
            settingsService.SaveValues(paths.SettingsPath,
                new Dictionary<string, object?>
                {
                    [OneWareCloudIntegrationModule.OneWareAccountUserIdKey] =
                        settingsService.GetSettingValue<string>(OneWareCloudIntegrationModule.OneWareAccountUserIdKey)
                }, autoSave: false);

            await NotifyRunningStudioAsync(cancellationToken);
            Console.WriteLine("Logged out of OneWare Cloud.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to persist OneWare Cloud logout: {ex.Message}");
            return 1;
        }
    }

    private static void EnsureSettingsInitialized(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        var paths = serviceProvider.GetRequiredService<IPaths>();

        if (!settingsService.HasSetting(OneWareCloudIntegrationModule.OneWareCloudHostKey))
            settingsService.Register(OneWareCloudIntegrationModule.OneWareCloudHostKey,
                OneWareCloudIntegrationModule.OfficialHost);

        if (!settingsService.HasSetting(OneWareCloudIntegrationModule.OneWareAccountUserIdKey))
            settingsService.Register(OneWareCloudIntegrationModule.OneWareAccountUserIdKey, string.Empty);

        settingsService.Load(paths.SettingsPath);
    }

    private static async Task NotifyRunningStudioAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", "oneware-studio-ipc", PipeDirection.Out);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(timeout.Token);

            await using var writer = new StreamWriter(client);
            await writer.WriteAsync(OneWareCloudIntegrationModule.LogoutIpcMessage.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Studio is not running or is not ready to receive IPC messages.
        }
        catch (IOException)
        {
            // Studio is not running or is not ready to receive IPC messages.
        }
    }
}
