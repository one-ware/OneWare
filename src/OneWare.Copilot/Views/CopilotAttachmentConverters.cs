using Avalonia.Data.Converters;

namespace OneWare.Copilot.Views;

public static class CopilotAttachmentConverters
{
    /// <summary>Dims attachment chips that won't be sent.</summary>
    public static readonly IValueConverter IncludedOpacity =
        new FuncValueConverter<bool, double>(included => included ? 1.0 : 0.45);
}
