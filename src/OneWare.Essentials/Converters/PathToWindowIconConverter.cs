using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace OneWare.Essentials.Converters;

public class PathToWindowIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return null;

        if (value is not string rawUri || !targetType.IsAssignableFrom(typeof(WindowIcon)))
            throw new NotSupportedException();
        Uri uri;

        // Allow for assembly overrides
        if (rawUri.StartsWith("avares://"))
        {
            uri = new Uri(rawUri);
        }
        else
        {
            var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
            uri = new Uri($"avares://{assemblyName}{rawUri}");
        }
            
        return new WindowIcon(new Bitmap(AssetLoader.Open(uri)));;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}