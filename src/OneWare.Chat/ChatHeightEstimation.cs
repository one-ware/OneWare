using System;

namespace OneWare.Chat;

/// <summary>
/// Heuristics for estimating a chat message's rendered pixel height from its markdown content,
/// before it has been realized and measured. Used to give the virtualizing message list a stable,
/// content-accurate scroll extent.
/// </summary>
public static class ChatHeightEstimation
{
    private const double CharWidth = 7.5;
    private const double LineHeight = 18;
    private const double VerticalPadding = 16;

    /// <summary>
    /// Estimates the height of a block of markdown/plain text for the given available width by
    /// approximating wrapped line counts. The real measured height replaces this once realized,
    /// so the estimate only needs to be in the right ballpark.
    /// </summary>
    public static double EstimateMarkdown(string? text, double width, double extra = 0)
    {
        if (string.IsNullOrEmpty(text))
            return VerticalPadding + LineHeight;

        var charsPerLine = Math.Max(1, width / CharWidth);
        double lines = 0;

        foreach (var hardLine in text.Split('\n'))
            lines += Math.Max(1, Math.Ceiling(hardLine.Length / charsPerLine));

        return VerticalPadding + extra + lines * LineHeight;
    }
}
