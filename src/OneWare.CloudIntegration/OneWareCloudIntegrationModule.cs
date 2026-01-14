using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;
using OneWare.CloudIntegration.ViewModels;
using OneWare.CloudIntegration.Views;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.CloudIntegration;

public class OneWareCloudIntegrationModule : IModule
{
    public const string OneWareCloudHostKey = "General_OneWareCloud_Host";
    public const string OneWareAccountUserIdKey = "General_OneWareCloud_AccountUserId";
    public const string CredentialStore = "https://cloud.one-ware.com";
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<OneWareCloudAccountSetting>();
        containerRegistry.RegisterSingleton<OneWareCloudLoginService>();
        containerRegistry.RegisterSingleton<OneWareCloudNotificationService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice");

        containerProvider.Resolve<ISettingsService>().RegisterSetting("General", "OneWare Cloud", OneWareCloudHostKey, new TextBoxSetting("Host", "https://cloud.one-ware.com", "https://cloud.one-ware.com"));
        containerProvider.Resolve<ISettingsService>().RegisterCustom("General", "OneWare Cloud", OneWareAccountUserIdKey, containerProvider.Resolve<OneWareCloudAccountSetting>());
        containerProvider.Resolve<IWindowService>().RegisterUiExtension("MainWindow_BottomRightExtension", new UiExtension(x =>
            new CloudIntegrationMainWindowBottomRightExtension()
            {
                DataContext = containerProvider.Resolve<CloudIntegrationMainWindowBottomRightExtensionViewModel>()
            }));
        
        
        containerProvider.Resolve<IWindowService>().RegisterUiExtension("MainWindow_RightToolBarExtension", new UiExtension(x =>
            new OneWareCloudAccountFlyoutView()
            {
                DataContext = containerProvider.Resolve<OneWareCloudAccountFlyoutViewModel>()
            }));

        containerProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemViewModel("Feedback")
        {
            Header = "Send Feedback",
            IconObservable = Application.Current!.GetResourceObservable("Unicons.CommentMessage"),
            Command = new AsyncRelayCommand(async () => await OpenFeedbackDialogAsync())
        });

    }
    
    public static async Task OpenFeedbackDialogAsync()
    {
        var windowService = ContainerLocator.Container.Resolve<IWindowService>();
        var loginService = ContainerLocator.Container.Resolve<OneWareCloudLoginService>();
        var accountSetting = ContainerLocator.Container.Resolve<OneWareCloudAccountSetting>();
                    
        var dataContext = new FeedbackViewModel(loginService, accountSetting);
        await windowService.ShowDialogAsync(new SendFeedbackView()
        {
            DataContext = dataContext
        });

        if (!dataContext.Result.HasValue)
            return;
            
        string msg = dataContext.Result == true
            ? "We received your feedback and process the request as soon as possible." 
            : "Something went wrong. Please retry it later.";
        await windowService.ShowMessageAsync("Feedback sent", msg, MessageBoxIcon.Info);
    }
}