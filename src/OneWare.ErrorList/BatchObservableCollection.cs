using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace OneWare.ErrorList;

/// <summary>
///     ObservableCollection that can suppress change notifications while a batch of mutations is applied and
///     raises a single Reset afterwards.
///     This is required for <see cref="Avalonia.Collections.DataGridCollectionView" />, which crashes
///     (ArgumentOutOfRangeException in AdjustCurrencyForRemove) when an item that is filtered out of the view is
///     removed while the current position is 0. Handling a Reset makes the view rebuild itself safely.
/// </summary>
internal class BatchObservableCollection<T> : ObservableCollection<T>
{
    private bool _isDirty;
    private int _suspendLevel;

    public IDisposable BeginBatch()
    {
        _suspendLevel++;
        return new BatchScope(this);
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suspendLevel > 0)
        {
            _isDirty = true;
            return;
        }

        base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_suspendLevel > 0) return;

        base.OnPropertyChanged(e);
    }

    private void EndBatch()
    {
        if (_suspendLevel == 0) return;

        _suspendLevel--;
        if (_suspendLevel > 0 || !_isDirty) return;

        _isDirty = false;
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private sealed class BatchScope(BatchObservableCollection<T> owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner.EndBatch();
        }
    }
}
