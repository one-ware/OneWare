using Avalonia.Data.Converters;

namespace OneWare.Core.Converters;

public static class CoreConverters
{
    public static readonly IValueConverter FileToTabBackgroundConverter = new FileToTabBackgroundConverter();
}