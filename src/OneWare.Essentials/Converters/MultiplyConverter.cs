using System.Globalization;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters
{
    public class MultiplyConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var result = 0.0;
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] is IConvertible convert)
                {
                    if (i == 0)
                        result = convert.ToDouble(null);
                    else
                        result *= convert.ToDouble(null);
                }
            }

            return Math.Ceiling(result); //Can't render smaller than 1
        }

        public object ConvertBack(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}