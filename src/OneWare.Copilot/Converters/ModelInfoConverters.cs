using System.Globalization;
using Avalonia.Data.Converters;
using GitHub.Copilot;

namespace OneWare.Copilot.Converters;

/// <summary>
///     Formats a token count into a compact human readable string, e.g. 128000 -> "128K", 1000000 -> "1M".
/// </summary>
public class TokenCountToCompactStringConverter : IValueConverter
{
    public static readonly TokenCountToCompactStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return null;

        double tokens;
        try
        {
            tokens = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }

        if (tokens <= 0) return null;

        return Format(tokens);
    }

    private static string Format(double tokens)
    {
        if (tokens >= 1_000_000)
        {
            var millions = tokens / 1_000_000;
            return $"{millions.ToString(millions % 1 == 0 ? "0" : "0.#", CultureInfo.InvariantCulture)}M";
        }

        if (tokens >= 1_000)
        {
            var thousands = tokens / 1_000;
            return $"{thousands.ToString(thousands % 1 == 0 ? "0" : "0.#", CultureInfo.InvariantCulture)}K";
        }

        return tokens.ToString("0", CultureInfo.InvariantCulture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
///     Formats a <see cref="ModelInfo" />'s token pricing into a short string expressed in AI Credits
///     per 1M tokens, e.g. "in 1.25 / out 5.00 cr·1M". Returns an empty string when no pricing is available.
/// </summary>
public class ModelPriceConverter : IValueConverter
{
    public static readonly ModelPriceConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ModelInfo model) return string.Empty;

        var prices = model.Billing?.TokenPrices;
        if (prices == null) return string.Empty;

        var batchSize = prices.BatchSize is > 0 ? (double)prices.BatchSize.Value : 1d;

        var input = NormalizePerMillion(prices.InputPrice, batchSize);
        var output = NormalizePerMillion(prices.OutputPrice, batchSize);

        if (input == null && output == null) return string.Empty;

        var parts = new List<string>();
        if (input != null) parts.Add($"{input.Value.ToString("0.##", CultureInfo.InvariantCulture)} in");
        if (output != null) parts.Add($"{output.Value.ToString("0.##", CultureInfo.InvariantCulture)} out");

        return $"credits per 1M: {string.Join(", ", parts)}";
    }

    private static double? NormalizePerMillion(double? price, double batchSize)
    {
        if (price == null) return null;
        return price.Value / batchSize * 1_000_000d;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
///     Produces a verbose tooltip for a <see cref="ModelInfo" /> describing the exact context window
///     and token pricing.
/// </summary>
public class ModelInfoTooltipConverter : IValueConverter
{
    public static readonly ModelInfoTooltipConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ModelInfo model) return null;

        var lines = new List<string> { model.Name };

        var context = model.Capabilities.Limits.MaxContextWindowTokens;
        if (context > 0)
            lines.Add($"Context window: {context.ToString("N0", CultureInfo.InvariantCulture)} tokens");

        var prices = model.Billing?.TokenPrices;
        if (prices != null)
        {
            var batchSize = prices.BatchSize is > 0 ? (double)prices.BatchSize.Value : 1d;

            if (prices.InputPrice is { } inPrice)
                lines.Add(
                    $"Input: {(inPrice / batchSize * 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture)} credits / 1M tokens");
            if (prices.OutputPrice is { } outPrice)
                lines.Add(
                    $"Output: {(outPrice / batchSize * 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture)} credits / 1M tokens");
        }

        return string.Join("\n", lines);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
