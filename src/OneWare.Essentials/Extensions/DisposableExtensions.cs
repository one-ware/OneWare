using System.Reactive.Disposables;

namespace OneWare.Essentials.Extensions;

public static class DisposableExtensions
{
    /// <summary>
    ///     Adds <paramref name="item" /> to <paramref name="compositeDisposable" /> and returns it.
    /// </summary>
    public static T DisposeWith<T>(this T item, CompositeDisposable compositeDisposable) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(compositeDisposable);
        compositeDisposable.Add(item);
        return item;
    }
}
