using System.Collections.ObjectModel;

namespace OneWare.Core.Extensions;

public static class ListExtensions
{
    /// <summary>
    ///     Merges two lists together without replacing original values
    ///     All new elements will added to the tail. All elements not existing in collection2 will be removed from collection1!
    /// </summary>
    public static void Merge(this Collection<string> collection1, Collection<string> collection2)
    {
        var diff = new List<string>();

        //Add all new elements to diff list
        for (var i = 0; i < collection2.Count; i++)
            if (!collection1.Contains(collection2[i]))
                diff.Add(collection2[i]);

        //remove all deleted elements
        for (var i = 0; i < collection1.Count; i++)
            if (!collection2.Contains(collection1[i]))
            {
                collection1.RemoveAt(i);
                i--;
            }

        //Add new elements to end of old list
        collection1.AddRange(diff);
    }

    public static int Remove<T>(
        this ObservableCollection<T> coll, Func<T, bool> condition)
    {
        var itemsToRemove = coll.Where(condition).ToList();

        foreach (var itemToRemove in itemsToRemove) coll.Remove(itemToRemove);

        return itemsToRemove.Count;
    }
}