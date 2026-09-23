using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Simple;
using OneWare.PackageManager.UnitTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace OneWare.PackageManager.UnitTests;

/// <summary>
///     Minimal application for the headless tests. The package view models resolve theme brushes from
///     <see cref="Application.Current" />, so a running application is required.
/// </summary>
public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new SimpleTheme());
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
