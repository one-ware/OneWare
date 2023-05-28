using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace OneWare.Shared.Converters
{
    public class PathToBitmapConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value == null)
                return null;

            if (value is not string rawUri || !targetType.IsAssignableFrom(typeof(Bitmap)))
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

            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
            if (assets == null) return null;
            var asset = assets.Open(uri);

            return new Bitmap(asset);

        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}