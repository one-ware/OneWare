using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OneWare.Core.Data;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Views.Windows;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.PackageManager;
using OneWare.SourceControl;
using OneWare.TerminalManager;

namespace OneWare.Demo.Desktop;

public class DesktopDemoApp : DemoApp
{
    protected override void ConfigureModuleCatalog(OneWareModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);

        moduleCatalog.AddModule<PackageManagerModule>();
        moduleCatalog.AddModule<TerminalManagerModule>();
        moduleCatalog.AddModule<SourceControlModule>();
    }

    protected override async Task LoadContentAsync()
    {
        await base.LoadContentAsync();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            var key = Services.Resolve<IApplicationStateService>()
                .AddState("Loading last projects...", AppState.Loading);
            await Services.Resolve<IProjectExplorerService>().OpenLastProjectsFileAsync();
            Services.Resolve<IMainDockService>().InitializeContent();
            Services.Resolve<IApplicationStateService>().RemoveState(key, "Projects loaded!");
        }
        
        try
        {
            var settingsService = Services.Resolve<ISettingsService>();
            Services.Resolve<ILogger>()?.Log("Loading last projects finished");

            if (settingsService.GetSettingValue<string>("LastVersion") != Global.VersionCode)
            {
                settingsService.SetSettingValue("LastVersion", Global.VersionCode);

                Services.Resolve<IWindowService>().ShowNotificationWithButton("Update Successful!",
                    $"{Services.Resolve<IPaths>().AppName} got updated to {Global.VersionCode}!", "View Changelog",
                    () => { Services.Resolve<IWindowService>().Show(new ChangelogView()); },
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
            }
        }
        catch (Exception e)
        {
            Services.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}