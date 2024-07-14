namespace OneWare.Vcd.Parser.Extensions;

public static class ListExtensions
{
    public static int BinarySearchIndex<TItem, TSearch>(this IList<TItem> list, TSearch value,
        Func<TSearch, int, int, int> comparer)
    {
        if (list is null) throw new ArgumentNullException(nameof(list));

        if (comparer is null) throw new ArgumentNullException(nameof(comparer));

        var lower = 0;
        var upper = list.Count - 1;

        while (lower <= upper)
        {
            var middle = lower + (upper - lower) / 2;
            var comparisonResult = comparer(value, middle, middle + 1);
            if (comparisonResult < 0)
                upper = middle - 1;
            else if (comparisonResult > 0)
                lower = middle + 1;
            else
                return middle;
        }

        return ~lower;
    }

    public static int BinarySearchNext<TItem, TSearch>(this IList<TItem> list, TSearch value,
        Func<TSearch, TItem, TItem, int> comparer)
    {
        if (list is null) throw new ArgumentNullException(nameof(list));

        if (comparer is null) throw new ArgumentNullException(nameof(comparer));

        var lower = 0;
        var upper = list.Count - 1;

        while (lower <= upper)
        {
            var middle = lower + (upper - lower) / 2;
            var comparisonResult =
                comparer(value, list[middle], middle + 1 < list.Count ? list[middle + 1] : list[middle]);
            if (comparisonResult < 0)
                upper = middle - 1;
            else if (comparisonResult > 0)
                lower = middle + 1;
            else
                return middle;
        }

        return ~lower;
    }
}