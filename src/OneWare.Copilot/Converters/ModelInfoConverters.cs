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
///     Formats a single context-length tier of a <see cref="ModelInfo" /> as a compact one-line summary,
///     e.g. "Default · ≤128K". The <c>parameter</c> selects the tier: "default" (standard window) or
///     "long" (extended long-context window). Returns <c>null</c> when the requested tier is not available
///     for the model (e.g. a model without a long-context tier). Pricing is intentionally omitted here and
///     surfaced through <see cref="ModelInfoTooltipConverter" /> instead.
/// </summary>
public class ModelContextTierConverter : IValueConverter
{
    public static readonly ModelContextTierConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ModelInfo model) return null;

        var isLong = string.Equals(parameter as string, "long", StringComparison.OrdinalIgnoreCase);

        var prices = model.Billing?.TokenPrices;
        var longContext = prices?.LongContext;

        // A model only exposes the "Long" tier when long-context pricing is present.
        if (isLong && longContext == null) return null;

        long? windowTokens;
        string label;

        if (isLong)
        {
            label = "Long";
            windowTokens = longContext!.MaxPromptTokens ?? model.Capabilities.Limits.MaxContextWindowTokens;
        }
        else
        {
            // When a long-context tier exists, the standard tier only applies up to MaxPromptTokens;
            // otherwise the standard window is the model's full context window.
            label = longContext != null ? "Default" : "Context";
            windowTokens = longContext != null
                ? prices?.MaxPromptTokens
                : prices?.MaxPromptTokens ?? model.Capabilities.Limits.MaxContextWindowTokens;
            windowTokens ??= model.Capabilities.Limits.MaxContextWindowTokens;
        }

        return windowTokens is > 0 ? $"{label} · ≤{FormatTokens(windowTokens.Value)}" : null;
    }

    internal static string FormatTokens(double tokens)
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
///     Structured, render-friendly description of a single context tier's pricing, produced by
///     <see cref="ModelTooltipInfoConverter" /> for the custom model tooltip.
/// </summary>
public sealed class ModelTierPricing
{
    public string HeaderText { get; init; } = string.Empty;
    public string? InputText { get; init; }
    public string? OutputText { get; init; }
    public bool HasInput => !string.IsNullOrEmpty(InputText);
    public bool HasOutput => !string.IsNullOrEmpty(OutputText);
}

/// <summary>
///     Structured, render-friendly description of a <see cref="ModelInfo" />, produced by
///     <see cref="ModelTooltipInfoConverter" /> and rendered by the custom model tooltip template.
/// </summary>
public sealed class ModelTooltipInfo
{
    public string Name { get; init; } = string.Empty;
    public string? Id { get; init; }
    public bool HasId => !string.IsNullOrEmpty(Id) && Id != Name;
    public string? ContextWindow { get; init; }
    public bool HasContextWindow => !string.IsNullOrEmpty(ContextWindow);
    public IReadOnlyList<ModelTierPricing> Tiers { get; init; } = [];
    public bool HasTiers => Tiers.Count > 0;
}

/// <summary>
///     Converts a <see cref="ModelInfo" /> into a <see cref="ModelTooltipInfo" /> describing the context
///     window and token pricing for both the default and (when available) long-context tiers.
/// </summary>
public class ModelTooltipInfoConverter : IValueConverter
{
    public static readonly ModelTooltipInfoConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ModelInfo model) return null;

        var context = model.Capabilities.Limits.MaxContextWindowTokens;
        var tiers = new List<ModelTierPricing>();

        var prices = model.Billing?.TokenPrices;
        if (prices != null)
        {
            var batchSize = prices.BatchSize is > 0 ? (double)prices.BatchSize.Value : 1d;
            var longContext = prices.LongContext;

            var defaultHeader = longContext != null ? "Default context" : "Pricing";
            var defaultTier = BuildTier(defaultHeader, prices.MaxPromptTokens,
                prices.InputPrice, prices.OutputPrice, batchSize);
            if (defaultTier != null) tiers.Add(defaultTier);

            if (longContext != null)
            {
                var longTier = BuildTier("Long context",
                    longContext.MaxPromptTokens ?? context,
                    longContext.InputPrice, longContext.OutputPrice, batchSize);
                if (longTier != null) tiers.Add(longTier);
            }
        }

        return new ModelTooltipInfo
        {
            Name = model.Name,
            Id = model.Id,
            ContextWindow = context > 0
                ? $"{context.ToString("N0", CultureInfo.InvariantCulture)} tokens"
                : null,
            Tiers = tiers
        };
    }

    private static ModelTierPricing? BuildTier(string header, long? maxTokens,
        double? inputPrice, double? outputPrice, double batchSize)
    {
        var input = FormatCredits(inputPrice, batchSize);
        var output = FormatCredits(outputPrice, batchSize);
        if (input == null && output == null) return null;

        var suffix = maxTokens is > 0
            ? $" · up to {ModelContextTierConverter.FormatTokens(maxTokens.Value)}"
            : string.Empty;

        return new ModelTierPricing
        {
            HeaderText = $"{header}{suffix}",
            InputText = input,
            OutputText = output
        };
    }

    private static string? FormatCredits(double? price, double batchSize)
    {
        if (price is not { } value) return null;
        var perMillion = value / batchSize * 1_000_000d;
        return $"{perMillion.ToString("0.##", CultureInfo.InvariantCulture)} cr / 1M";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
