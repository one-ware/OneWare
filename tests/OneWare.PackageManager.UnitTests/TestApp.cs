using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;

[assembly: AvaloniaTestApplication(typeof(OneWare.PackageManager.UnitTests.TestAppBuilder))]

namespace OneWare.PackageManager.UnitTests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new Avalonia.Themes.Simple.SimpleTheme());
        Resources["ThemeBorderMidBrush"] = Brushes.Gray;
        Resources["ThemeAccentBrush"] = Brushes.SteelBlue;
        Resources["ThemeControlMidBrush"] = Brushes.LightGray;
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
