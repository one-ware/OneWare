using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Extensions;

public static class ObservableCollectionExtensions
{
    private const string PathSeparator = "\u2192";
    
    public static IDisposable WatchTreeChanges<T>(this ObservableCollection<T> collection, Action<T, string> onAdded, Action<T, string> onRemoved) where T : ICanHaveObservableItems<T>
    {
        var disposable = new CompositeDisposable();
        
        WatchTreeChangesChildren(collection, onAdded, onRemoved, disposable, new Dictionary<T, IDisposable>());
        
        return disposable;
    }
    
    private static IDisposable WatchTreeChangesChildren<T>(ObservableCollection<T> collection, Action<T, string> onAdded, Action<T, string> onRemoved, CompositeDisposable compositeDisposable, Dictionary<T, IDisposable> disposableDictionary, string path = "") where T : ICanHaveObservableItems<T>
    {
        var subDisposable = new CompositeDisposable();
        subDisposable.DisposeWith(compositeDisposable);
        
        //Add existing
        foreach (var item in collection)
        {
            onAdded(item, path);
            if (item.Items != null)
            {
                var s = WatchTreeChangesChildren(item.Items, onAdded, onRemoved, compositeDisposable, disposableDictionary, path);
                disposableDictionary[item] = s;
            }
        }
        
        //Track changes
        Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(collection, nameof(collection.CollectionChanged)).Subscribe(args =>
        {
            if (args.EventArgs.NewItems != null)
            {
                foreach (var item in args.EventArgs.NewItems)
                {
                    if (item is T typeItem)
                    {
                        onAdded.Invoke(typeItem, path);

                        typeItem.WhenValueChanged(x => x.Items).Subscribe(x =>
                        {
                            if (x != null)
                            {
                                var s = WatchTreeChangesChildren(x, onAdded, onRemoved, compositeDisposable, disposableDictionary, $"{path}{typeItem.Name} {PathSeparator} ");
                                disposableDictionary[typeItem] = s;
                            }
                        }).DisposeWith(subDisposable);
                    }
                }
            }
            if (args.EventArgs.OldItems != null)
            {
                foreach (var item in args.EventArgs.OldItems)
                {
                    if (item is T typeItem)
                    {
                        CallRemoveRecursive(typeItem, onRemoved, disposableDictionary, path);
                    }
                }
            }
        }).DisposeWith(subDisposable);
        return subDisposable;
    }
    
    private static void CallRemoveRecursive<T>(T item, Action<T, string> onRemoved, Dictionary<T, IDisposable> disposableDictionary, string path = "") where T : ICanHaveObservableItems<T>
    {
        onRemoved.Invoke(item, path);
        if(disposableDictionary.TryGetValue(item, out var disposable))
        {
            disposable.Dispose();
            disposableDictionary.Remove(item);
        }
        if (item.Items != null)
        {
            foreach (var i in item.Items)
            {
                CallRemoveRecursive<T>(i, onRemoved, disposableDictionary, $"{path}{i.Name} {PathSeparator} ");
            }
        }
    }
}