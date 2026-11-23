using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using Avalonia.Threading;

namespace OneWare.Essentials.Helpers;

/// <summary>
///     Rx scheduler that runs work on the Avalonia UI thread, e.g. <c>.ObserveOn(UiScheduler.Instance)</c>.
/// </summary>
public sealed class UiScheduler : LocalScheduler
{
    private UiScheduler()
    {
    }

    public static UiScheduler Instance { get; } = new();

    public override IDisposable Schedule<TState>(TState state, TimeSpan dueTime,
        Func<IScheduler, TState, IDisposable> action)
    {
        var result = new SingleAssignmentDisposable();

        void Run()
        {
            if (!result.IsDisposed) result.Disposable = action(this, state);
        }

        var delay = Scheduler.Normalize(dueTime);
        if (delay == TimeSpan.Zero)
        {
            Dispatcher.UIThread.Post(Run);
            return result;
        }

        return new CompositeDisposable(DispatcherTimer.RunOnce(Run, delay), result);
    }
}
