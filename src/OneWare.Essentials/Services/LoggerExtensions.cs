using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;

namespace OneWare.Essentials.Services;

public static class LoggerExtensions
{
    public static bool LayoutLoaded { get; set; } = false;

    public static void Log(this ILogger logger, object message, bool showOutput = false, IBrush? outputBrush = null)
    {
        var text = message?.ToString() ?? string.Empty;
        logger.LogInformation(text);

        if (showOutput && ContainerLocator.Container?.IsRegistered<IOutputService>() == true)
            ContainerLocator.Current.Resolve<IOutputService>().WriteLine(text, outputBrush);
    }

    public static void Warning(this ILogger logger, string message, Exception? exception = null, bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null)
    {
        logger.LogWarning(exception, message);
        WriteToOutput(message, showOutput, Brushes.Orange);
        ShowDialog(message, exception, showDialog, "Warning", MessageBoxIcon.Warning, dialogOwner);
    }

    public static void Error(this ILogger logger, string message, Exception? exception = null, bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null)
    {
        logger.LogError(exception, message);
        WriteToOutput(message, showOutput, Brushes.Red);
        ShowDialog(message, exception, showDialog, "Error", MessageBoxIcon.Error, dialogOwner);
    }

    private static void WriteToOutput(string message, bool showOutput, IBrush brush)
    {
        if (!showOutput || ContainerLocator.Container?.IsRegistered<IOutputService>() != true)
            return;

        ContainerLocator.Current.Resolve<IOutputService>().WriteLine(message, brush);

        if (LayoutLoaded)
            ContainerLocator.Current.Resolve<IMainDockService>()
                .Show(ContainerLocator.Current.Resolve<IOutputService>(), DockShowLocation.Bottom);
    }

    private static void ShowDialog(string message, Exception? exception, bool showDialog, string title,
        MessageBoxIcon icon, Window? owner)
    {
        if (!showDialog || ContainerLocator.Container?.IsRegistered<IWindowService>() != true)
            return;

        var output = exception == null ? message : $"{message}\n{exception.Message}";
        _ = ContainerLocator.Current.Resolve<IWindowService>().ShowMessageAsync(title, output, icon, owner);
    }
}