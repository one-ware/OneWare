using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Simple;
using OneWare.WaveFormViewer.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace OneWare.WaveFormViewer.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new SimpleTheme());
        Styles.Add(new StyleInclude(new Uri("avares://OneWare.WaveFormViewer.Tests"))
        {
            Source = new Uri("avares://OneWare.Essentials/Styles/OneWareTheme.axaml")
        });
    }
}

public static class TestAppBuilder
{
    // Skia rendering is required to export images from the waveform view.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
