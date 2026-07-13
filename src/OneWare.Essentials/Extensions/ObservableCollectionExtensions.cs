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

    public static IDisposable WatchTreeChanges<T>(this ObservableCollection<T> collection, Action<T, string> onAdded,
        Action<T, string> onRemoved) where T : ICanHaveObservableItems<T>
    {
        var disposable = new CompositeDisposable();

        WatchTreeChangesChildren(collection, onAdded, onRemoved, disposable, new Dictionary<T, IDisposable>());

        return disposable;
    }

    private static IDisposable WatchTreeChangesChildren<T>(ObservableCollection<T> collection,
        Action<T, string> onAdded, Action<T, string> onRemoved, CompositeDisposable compositeDisposable,
        Dictionary<T, IDisposable> disposableDictionary, string path = "") where T : ICanHaveObservableItems<T>
    {
        var subDisposable = new CompositeDisposable();
        subDisposable.DisposeWith(compositeDisposable);

        //Add existing
        foreach (var item in collection)
        {
            onAdded(item, path);
            if (item.Items != null)
            {
                var s = WatchTreeChangesChildren(item.Items, onAdded, onRemoved, compositeDisposable,
                    disposableDictionary, path);
                disposableDictionary[item] = s;
            }
        }

        //Track changes
        Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(collection, nameof(collection.CollectionChanged))
            .Subscribe(args =>
            {
                if (args.EventArgs.NewItems != null)
                    foreach (var item in args.EventArgs.NewItems)
                        if (item is T typeItem)
                        {
                            onAdded.Invoke(typeItem, path);

                            // Observe the Items property directly via INotifyPropertyChanged.
                            // Using DynamicData's expression based WhenValueChanged here fails because
                            // the accessor is built against the open generic type parameter T, which
                            // makes DynamicData fall back to Convert.ChangeType and throw for non
                            // IConvertible values (e.g. ObservableCollection).
                            Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                                    h => typeItem.PropertyChanged += h,
                                    h => typeItem.PropertyChanged -= h)
                                .Where(e => e.EventArgs.PropertyName == nameof(ICanHaveObservableItems<T>.Items))
                                .Select(_ => typeItem.Items)
                                .StartWith(typeItem.Items)
                                .Subscribe(x =>
                                {
                                    if (x != null)
                                    {
                                        var s = WatchTreeChangesChildren(x, onAdded, onRemoved, compositeDisposable,
                                            disposableDictionary, $"{path}{typeItem.Name} {PathSeparator} ");
                                        disposableDictionary[typeItem] = s;
                                    }
                                }).DisposeWith(subDisposable);
                        }

                if (args.EventArgs.OldItems != null)
                    foreach (var item in args.EventArgs.OldItems)
                        if (item is T typeItem)
                            CallRemoveRecursive(typeItem, onRemoved, disposableDictionary, path);
            }).DisposeWith(subDisposable);
        return subDisposable;
    }

    private static void CallRemoveRecursive<T>(T item, Action<T, string> onRemoved,
        Dictionary<T, IDisposable> disposableDictionary, string path = "") where T : ICanHaveObservableItems<T>
    {
        onRemoved.Invoke(item, path);
        if (disposableDictionary.TryGetValue(item, out var disposable))
        {
            disposable.Dispose();
            disposableDictionary.Remove(item);
        }

        if (item.Items != null)
            foreach (var i in item.Items)
                CallRemoveRecursive(i, onRemoved, disposableDictionary, $"{path}{i.Name} {PathSeparator} ");
    }
}