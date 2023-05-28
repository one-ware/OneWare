using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace OneWare.Shared.Converters;

public class PathToWindowIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string st)
        {
            if (st.StartsWith("avares://"))
            {
                st = st.Remove(0, "avares://".Length);
                var nextSlash = st.IndexOf("/", StringComparison.Ordinal);
                if (nextSlash > -1)
                {
                    st = st.Remove(0, nextSlash + 1);
                }
                return new WindowIcon(st);
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}