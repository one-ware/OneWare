namespace OneWare.ErrorList;

internal static class CollectionSync
{
    // Above this many changes a single Reset is cheaper than individual notifications
    private const int ResetThreshold = 64;

    /// <summary>
    ///     Updates <paramref name="target" /> to match <paramref name="desired" /> (both in the same sort order) with
    ///     as few change notifications as possible. Existing items are kept, so their state (expansion, selection)
    ///     survives.
    /// </summary>
    public static void Sync<T, TKey>(BatchObservableCollection<T> target, IReadOnlyList<TKey> desired,
        Func<T, TKey> keyOf, Func<TKey, T> create, IEqualityComparer<TKey> comparer) where TKey : notnull
    {
        var desiredKeys = new HashSet<TKey>(desired, comparer);
        var kept = target.Where(x => desiredKeys.Contains(keyOf(x))).ToList();
        var changes = target.Count - kept.Count + desired.Count - kept.Count;
        var inOrder = IsInOrder(kept, desired, keyOf, comparer);
        if (changes == 0 && inOrder) return;

        if (changes > ResetThreshold || !inOrder)
        {
            var existing = kept.ToDictionary(keyOf, comparer);
            using (target.BeginBatch())
            {
                target.Clear();
                foreach (var key in desired) target.Add(existing.TryGetValue(key, out var item) ? item : create(key));
            }

            return;
        }

        for (var i = target.Count - 1; i >= 0; i--)
            if (!desiredKeys.Contains(keyOf(target[i])))
                target.RemoveAt(i);

        for (var i = 0; i < desired.Count; i++)
            if (i >= target.Count || !comparer.Equals(keyOf(target[i]), desired[i]))
                target.Insert(i, create(desired[i]));
    }

    private static bool IsInOrder<T, TKey>(List<T> kept, IReadOnlyList<TKey> desired, Func<T, TKey> keyOf,
        IEqualityComparer<TKey> comparer)
    {
        var d = 0;
        foreach (var item in kept)
        {
            var key = keyOf(item);
            while (d < desired.Count && !comparer.Equals(desired[d], key)) d++;
            if (d == desired.Count) return false;
            d++;
        }

        return true;
    }
}
