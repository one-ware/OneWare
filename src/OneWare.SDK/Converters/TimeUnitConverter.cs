using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace OneWare.SDK.Converters
{
    public class TimeUnitConverter : IValueConverter
    {
        public static TimeUnitConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is long fs)
            {
                var negative = fs < 0;
                fs = (long)BigInteger.Abs(fs);
                double drawNumber = fs / 1000f;
                var unitStr = " ps";

                switch (drawNumber)
                {
                    //s
                    case >= 1000000000000:
                        drawNumber /= 1000000000000;
                        unitStr = " s";
                        break;
                    //ms
                    case >= 1000000000:
                        drawNumber /= 1000000000;
                        unitStr = " ms";
                        break;
                    //us
                    case >= 1000000:
                        drawNumber /= 1000000;
                        unitStr = " us";
                        break;
                    //ns
                    case >= 1000:
                        drawNumber /= 1000;
                        unitStr = " ns";
                        break;
                }

                if (negative) drawNumber *= -1;

                var s = Math.Round(drawNumber, 3).ToString(CultureInfo.InvariantCulture);
                var st = s.Split('.');
                if (st.Length == 1) s += ".000";
                else if (st[1].Length < 3)
                    for (var i = 0; i < 3 - st[1].Length; i++)
                        s += "0";
                return s + " " + unitStr;
            }

            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string s) return 0;
            if (!long.TryParse(Regex.Replace(s, "[^0-9]", ""), out var time)) return 0;
            if (s.Contains("ps", StringComparison.OrdinalIgnoreCase)) time *= 1000;
            else if (s.Contains("ns", StringComparison.OrdinalIgnoreCase)) time *= 1000000;
            else if (s.Contains("us", StringComparison.OrdinalIgnoreCase)) time *= 1000000000;
            else if (s.Contains("ms", StringComparison.OrdinalIgnoreCase)) time *= 1000000000000;
            else if (s.Contains("s", StringComparison.OrdinalIgnoreCase)) time *= 1000000000000000;

            return time;
        }
    }
}