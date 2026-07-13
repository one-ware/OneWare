namespace OneWare.Essentials.Controls;

/// <summary>
/// Implemented by items that can provide an estimated pixel height before they are realized
/// and measured. Used by <see cref="EstimatingVirtualizingStackPanel"/> to compute a stable,
/// content-accurate scroll extent (e.g. estimating a chat message's height from its markdown).
/// </summary>
public interface IEstimatedHeightItem
{
    /// <summary>
    /// Returns an estimated height in pixels for the given available width.
    /// The estimate is replaced by the real measured height once the item is realized.
    /// </summary>
    double EstimateHeight(double width);
}
