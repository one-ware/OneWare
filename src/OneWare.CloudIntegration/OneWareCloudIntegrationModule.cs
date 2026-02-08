using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using OneWare.CloudIntegration.ViewModels;
using OneWare.CloudIntegration.Views;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.CloudIntegration;

public class OneWareCloudIntegrationModule : OneWareModuleBase
{
    public const string OneWareCloudHostKey = "General_OneWareCloud_Host";
    public const string OneWareAccountUserIdKey = "General_OneWareCloud_AccountUserId";
    public const string CredentialStore = "https://cloud.one-ware.com";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<OneWareCloudAccountSetting>();
        services.AddSingleton<OneWareCloudLoginService>();
        services.AddSingleton<OneWareCloudNotificationService>();
        services.AddSingleton<OneWareCloudCurrentAccountService>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice");

        serviceProvider.Resolve<ISettingsService>()
            .RegisterSetting("General", "OneWare Cloud", OneWareCloudHostKey,
                new TextBoxSetting("Host", "https://cloud.one-ware.com", "https://cloud.one-ware.com"));
        serviceProvider.Resolve<ISettingsService>().RegisterCustom("General", "OneWare Cloud", OneWareAccountUserIdKey,
            serviceProvider.Resolve<OneWareCloudAccountSetting>());
        serviceProvider.Resolve<IWindowService>().RegisterUiExtension("MainWindow_BottomRightExtension",
            new OneWareUiExtension(_ =>
                new CloudIntegrationMainWindowBottomRightExtension
                {
                    DataContext = serviceProvider.Resolve<CloudIntegrationMainWindowBottomRightExtensionViewModel>()
                }));


        serviceProvider.Resolve<IWindowService>().RegisterUiExtension("MainWindow_RightToolBarExtension",
            new OneWareUiExtension(_ =>
                new OneWareCloudAccountFlyoutView
                {
                    DataContext = serviceProvider.Resolve<OneWareCloudAccountFlyoutViewModel>()
                }));

        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/Help",
            new MenuItemViewModel("Feedback")
            {
                Header = "Send Feedback",
                IconModel = new IconModel("Unicons.CommentMessage"),
                Command = new AsyncRelayCommand(async () => await OpenFeedbackDialogAsync())
            });
    }

    public static async Task OpenFeedbackDialogAsync()
    {
        var windowService = ContainerLocator.Container.Resolve<IWindowService>();
        var loginService = ContainerLocator.Container.Resolve<OneWareCloudLoginService>();
        var accountSetting = ContainerLocator.Container.Resolve<OneWareCloudAccountSetting>();

        var dataContext = new FeedbackViewModel(loginService, accountSetting);
        await windowService.ShowDialogAsync(new SendFeedbackView
        {
            DataContext = dataContext
        });

        if (!dataContext.Result.HasValue)
            return;

        var msg = dataContext.Result == true
            ? "We received your feedback and process the request as soon as possible."
            : "Something went wrong. Please retry it later.";
        await windowService.ShowMessageAsync("Feedback sent", msg, MessageBoxIcon.Info);
    }
}