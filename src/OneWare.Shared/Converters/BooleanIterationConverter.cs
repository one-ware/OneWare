using System.Globalization;
using Avalonia.Animation;
using Avalonia.Data.Converters;

namespace OneWare.Shared.Converters
{
    public class BooleanIterationConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool and true) return IterationCount.Infinite;
            return new IterationCount(0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}