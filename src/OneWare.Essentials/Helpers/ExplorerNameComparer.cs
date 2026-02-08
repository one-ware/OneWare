namespace OneWare.Essentials.Helpers;

public sealed class ExplorerNameComparer : IComparer<string>
{
    public static ExplorerNameComparer Instance { get; } = new();

    private ExplorerNameComparer()
    {
    }

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        var i = 0;
        var j = 0;

        while (i < left.Length && j < right.Length)
        {
            var c1 = left[i];
            var c2 = right[j];

            var isDigit1 = char.IsDigit(c1);
            var isDigit2 = char.IsDigit(c2);

            if (isDigit1 && isDigit2)
            {
                var start1 = i;
                var start2 = j;

                while (i < left.Length && char.IsDigit(left[i])) i++;
                while (j < right.Length && char.IsDigit(right[j])) j++;

                var nonZero1 = start1;
                while (nonZero1 < i && left[nonZero1] == '0') nonZero1++;

                var nonZero2 = start2;
                while (nonZero2 < j && right[nonZero2] == '0') nonZero2++;

                var len1 = i - nonZero1;
                var len2 = j - nonZero2;

                if (len1 != len2) return len1.CompareTo(len2);

                for (var k = 0; k < len1; k++)
                {
                    var diff = left[nonZero1 + k] - right[nonZero2 + k];
                    if (diff != 0) return diff;
                }

                var totalLen1 = i - start1;
                var totalLen2 = j - start2;
                if (totalLen1 != totalLen2) return totalLen1.CompareTo(totalLen2);

                continue;
            }

            if (isDigit1 != isDigit2)
            {
                var diff = char.ToUpperInvariant(c1).CompareTo(char.ToUpperInvariant(c2));
                if (diff != 0) return diff;
                i++;
                j++;
                continue;
            }

            var segStart1 = i;
            var segStart2 = j;

            while (i < left.Length && !char.IsDigit(left[i])) i++;
            while (j < right.Length && !char.IsDigit(right[j])) j++;

            var segLen1 = i - segStart1;
            var segLen2 = j - segStart2;
            var cmp = string.Compare(left, segStart1, right, segStart2,
                Math.Min(segLen1, segLen2), StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;
            if (segLen1 != segLen2) return segLen1.CompareTo(segLen2);
        }

        return left.Length.CompareTo(right.Length);
    }
}
