using System.Collections.ObjectModel;

namespace OneWare.Essentials.Extensions;

public static class CollectionExtensions
{
    public static void Merge<T>(this Collection<T> collection1, IList<T> collection2, Func<T, T, bool> isEqual,
        Func<T, T, int> comparison)
    {
        //Remove all elements that are not in collection2
        for (var i = 0; i < collection1.Count; i++)
        {
            var existing = false;
            for (var b = 0; b < collection2.Count; b++)
                if (isEqual(collection1[i], collection2[b]))
                {
                    existing = true;
                    collection2.RemoveAt(b);
                    break;
                }

            if (!existing)
            {
                collection1.RemoveAt(i);
                i--;
            }
        }

        //Insert all new elements
        for (var i = 0; i < collection2.Count; i++)
        {
            var inserted = false;
            for (var b = 0; b < collection1.Count; b++)
            {
                var compare = comparison(collection2[i], collection1[b]);

                if (compare >= 0)
                {
                    collection1.Insert(b, collection2[i]);
                    inserted = true;
                    break;
                }
            }

            if (!inserted) collection1.Add(collection2[i]);
        }
    }
}