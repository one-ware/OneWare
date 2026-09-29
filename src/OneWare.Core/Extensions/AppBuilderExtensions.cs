using Avalonia;
using Avalonia.Media;

namespace OneWare.Core.Extensions;

public static class AppBuilderExtensions
{
    /// <summary>
    ///     Registers the bundled Inter font and picks the UI font: the system font (Segoe UI, SF Pro) on
    ///     Windows / macOS, Inter on Linux and in the browser, where the default font is unpredictable or missing.
    /// </summary>
    public static AppBuilder WithOneWareFonts(this AppBuilder builder)
    {
        builder = builder.WithInterFont();

        if (OperatingSystem.IsLinux() || OperatingSystem.IsBrowser())
            builder = builder.With(new FontManagerOptions
            {
                DefaultFamilyName = "fonts:Inter#Inter"
            });

        return builder;
    }
}
