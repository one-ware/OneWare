using Avalonia.Data.Converters;

namespace OneWare.UniversalFpgaProjectSystem.Converters;

public static class UniversalFpgaProjectSystemConverters
{
    public static readonly IValueConverter HorizontalLabelMarginConverter = new HorizontalLabelMarginConverter();
    public static readonly IValueConverter ObjectNotNullToStarGridLengthConverter = new ObjectNotNullToStarGridLengthConverter();

}