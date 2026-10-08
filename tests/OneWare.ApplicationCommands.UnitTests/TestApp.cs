using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Simple;
using OneWare.ApplicationCommands.UnitTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace OneWare.ApplicationCommands.UnitTests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new SimpleTheme());
        Styles.Add(new StyleInclude(new Uri("avares://OneWare.ApplicationCommands.UnitTests"))
        {
            Source = new Uri("avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml")
        });
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
