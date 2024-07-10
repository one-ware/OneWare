using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace OneWare.Essentials.Converters
{
    public class PathToBitmapConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value == null)
                return null;

            if (value is not string rawUri || !targetType.IsAssignableFrom(typeof(Bitmap)))
                throw new NotSupportedException();
            
            
            // Allow for assembly overrides
            if (rawUri.StartsWith("avares://"))
            {
                var uri = new Uri(rawUri);
                return new Bitmap(AssetLoader.Open(uri));
            }

            if (!File.Exists(rawUri)) return null;
            
            return new Bitmap(rawUri);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}