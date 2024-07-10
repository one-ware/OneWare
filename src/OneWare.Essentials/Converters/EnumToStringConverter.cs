using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace OneWare.Essentials.Converters
{
    public class EnumToStringConverter : IValueConverter
    {
        /// <summary>
        ///     Converts enum e.g ModifiedInWorkdir to "Modified in Workdir"
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var name = value?.ToString();
            if (!string.IsNullOrWhiteSpace(name)) name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            return name;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}