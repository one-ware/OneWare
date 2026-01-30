using System.Runtime.InteropServices;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Core;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Settings;

namespace OneWare.Demo;

public class DemoApp : App
{
    public static readonly ISettingsService SettingsService = new SettingsService();

    public static readonly IPaths Paths = new Paths("OneWare Demo", "avares://OneWare.Demo/Assets/icon.ico");

    static DemoApp()
    {
        SettingsService.Register("LastVersion", Global.VersionCode);
        SettingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
        SettingsService.RegisterSetting("Experimental", "Misc", "Experimental_UseManagedFileDialog",
            new CheckBoxSetting("Use Managed File Dialog", RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                HoverDescription = ""
            });
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton(SettingsService);
        services.AddSingleton(Paths);

        base.RegisterServices(services);
    }

    public override void Initialize()
    {
        var themeManager = new ThemeManager(SettingsService, Paths);
        base.Initialize();

        Styles.Add(new StyleInclude(new Uri("avares://OneWare.Demo"))
        {
            Source = new Uri("avares://OneWare.Demo/Styles/Theme.axaml")
        });
    }
}