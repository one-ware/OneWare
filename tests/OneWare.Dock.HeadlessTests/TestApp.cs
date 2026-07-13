using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using OneWareDockHeadlessTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace OneWareDockHeadlessTests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new global::Avalonia.Themes.Simple.SimpleTheme());
        Styles.Add(new StyleInclude(new System.Uri("resm:Styles?assembly=OneWareDockHeadlessTests"))
        {
            Source = new System.Uri("avares://Dock.Avalonia.Themes.Simple/DockSimpleTheme.axaml")
        });
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
