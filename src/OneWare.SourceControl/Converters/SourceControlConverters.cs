using Avalonia.Data.Converters;
using OneWare.Shared.Converters;

namespace OneWare.SourceControl.Converters;

public static class SourceControlConverters
{
    public static readonly IValueConverter ChangeStatusBrushConverter = new ChangeStatusBrushConverter();
    public static readonly IValueConverter ChangeStatusCharConverter = new ChangeStatusCharConverter();
}