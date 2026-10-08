using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Extensions;

public static class ObservableCollectionExtensions
{
    private const string PathSeparator = "\u2192";

    /// <summary>
    /// Watches a tree of observable collections. <paramref name="onAdded" /> is called for every item that is
    /// in the tree when watching starts and for every item added later at any depth, <paramref name="onRemoved" />
    /// for every item that leaves the tree. Replacing the <see cref="ICanHaveObservableItems{T}.Items" />
    /// collection of an item counts as removing the old children and adding the new ones.
    /// The string argument is the path of the parent items.
    /// </summary>
    public static IDisposable WatchTreeChanges<T>(this ObservableCollection<T> collection, Action<T, string> onAdded,
        Action<T, string> onRemoved) where T : ICanHaveObservableItems<T>
    {
        var watchedItems = new Dictionary<T, ItemWatch>();
        var rootSubscription = WatchCollection(collection, onAdded, onRemoved, watchedItems, "");

        return Disposable.Create(() =>
        {
            rootSubscription.Dispose();
            foreach (var watch in watchedItems.Values.ToList()) watch.Dispose();
            watchedItems.Clear();
        });
    }

    private static IDisposable WatchCollection<T>(ObservableCollection<T> collection, Action<T, string> onAdded,
        Action<T, string> onRemoved, Dictionary<T, ItemWatch> watchedItems, string path)
        where T : ICanHaveObservableItems<T>
    {
        foreach (var item in collection.ToList())
            WatchItem(item, onAdded, onRemoved, watchedItems, path);

        return Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(collection,
                nameof(collection.CollectionChanged))
            .Subscribe(args =>
            {
                if (args.EventArgs.OldItems != null)
                    foreach (var item in args.EventArgs.OldItems)
                        if (item is T typeItem)
                            UnwatchItem(typeItem, onRemoved, watchedItems, path);

                if (args.EventArgs.NewItems != null)
                    foreach (var item in args.EventArgs.NewItems)
                        if (item is T typeItem)
                            WatchItem(typeItem, onAdded, onRemoved, watchedItems, path);
            });
    }

    private static void WatchItem<T>(T item, Action<T, string> onAdded, Action<T, string> onRemoved,
        Dictionary<T, ItemWatch> watchedItems, string path) where T : ICanHaveObservableItems<T>
    {
        // An item can only be watched once, e.g. if it gets moved within the same collection
        if (watchedItems.TryGetValue(item, out var existing))
        {
            UnwatchItem(item, onRemoved, watchedItems, existing.Path);
        }

        onAdded(item, path);

        var watch = new ItemWatch(path);
        watchedItems[item] = watch;

        var childPath = $"{path}{item.Name} {PathSeparator} ";
        var watchedChildren = item.Items;
        if (watchedChildren != null)
            watch.Children.Disposable =
                WatchCollection(watchedChildren, onAdded, onRemoved, watchedItems, childPath);

        // Observe the Items property directly via INotifyPropertyChanged.
        // Using DynamicData's expression based WhenValueChanged here fails because
        // the accessor is built against the open generic type parameter T, which
        // makes DynamicData fall back to Convert.ChangeType and throw for non
        // IConvertible values (e.g. ObservableCollection).
        watch.ItemsProperty = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                h => item.PropertyChanged += h,
                h => item.PropertyChanged -= h)
            .Where(e => e.EventArgs.PropertyName == nameof(ICanHaveObservableItems<T>.Items))
            .Subscribe(_ =>
            {
                if (ReferenceEquals(item.Items, watchedChildren)) return;

                watch.Children.Disposable = null;
                if (watchedChildren != null)
                    foreach (var child in watchedChildren.ToList())
                        UnwatchItem(child, onRemoved, watchedItems, childPath);

                watchedChildren = item.Items;
                if (watchedChildren != null)
                    watch.Children.Disposable =
                        WatchCollection(watchedChildren, onAdded, onRemoved, watchedItems, childPath);
            });
    }

    private static void UnwatchItem<T>(T item, Action<T, string> onRemoved,
        Dictionary<T, ItemWatch> watchedItems, string path) where T : ICanHaveObservableItems<T>
    {
        if (!watchedItems.Remove(item, out var watch)) return;
        watch.Dispose();

        onRemoved(item, path);

        if (item.Items != null)
            foreach (var child in item.Items.ToList())
                UnwatchItem(child, onRemoved, watchedItems, $"{path}{item.Name} {PathSeparator} ");
    }

    private sealed class ItemWatch(string path) : IDisposable
    {
        public string Path { get; } = path;
        public SerialDisposable Children { get; } = new();
        public IDisposable? ItemsProperty { get; set; }

        public void Dispose()
        {
            ItemsProperty?.Dispose();
            Children.Dispose();
        }
    }
}