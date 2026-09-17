using System.Collections.ObjectModel;

namespace OneWare.PackageManager.ViewModels;

public static class ObservableCollectionReconciler
{
    /// <summary>
    ///     Brings <paramref name="target" /> in line with <paramref name="desired" /> using the fewest
    ///     notifications.
    ///     <para>
    ///         As long as the relative order of the surviving items is unchanged, only removals and
    ///         insertions are emitted, which keeps a bound list box's selection and scroll position
    ///         intact. A genuine reorder falls back to a reset, which is acceptable because it can only
    ///         result from a user triggered relayout.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     Items are matched by the default comparer. The package view models do not override Equals, so
    ///     that is reference equality. A duplicate in <paramref name="desired" /> cannot be matched
    ///     unambiguously and falls back to a reset.
    /// </remarks>
    public static void Reconcile<T>(ObservableCollection<T> target, IReadOnlyList<T> desired) where T : notnull
    {
        var desiredIndex = new Dictionary<T, int>(desired.Count);
        for (var i = 0; i < desired.Count; i++)
            desiredIndex[desired[i]] = i;

        if (desiredIndex.Count != desired.Count)
        {
            Reset(target, desired);
            return;
        }

        for (var i = target.Count - 1; i >= 0; i--)
            if (!desiredIndex.ContainsKey(target[i]))
                target.RemoveAt(i);

        var ordered = true;
        var previous = -1;

        foreach (var item in target)
        {
            var index = desiredIndex[item];
            if (index < previous)
            {
                ordered = false;
                break;
            }

            previous = index;
        }

        if (!ordered)
        {
            Reset(target, desired);
            return;
        }

        for (var i = 0; i < desired.Count; i++)
            if (i >= target.Count || !EqualityComparer<T>.Default.Equals(target[i], desired[i]))
                target.Insert(i, desired[i]);
    }

    private static void Reset<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
    {
        target.Clear();
        foreach (var item in desired) target.Add(item);
    }
}
