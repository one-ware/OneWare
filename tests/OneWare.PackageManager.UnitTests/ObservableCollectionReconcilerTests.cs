using System.Collections.ObjectModel;
using System.Collections.Specialized;
using OneWare.PackageManager.ViewModels;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class ObservableCollectionReconcilerTests
{
    private static (ObservableCollection<string> Collection, List<NotifyCollectionChangedAction> Actions) Track(
        params string[] initial)
    {
        var collection = new ObservableCollection<string>(initial);
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);
        return (collection, actions);
    }

    [Fact]
    public void Reconcile_WithIdenticalContent_DoesNotNotify()
    {
        var (collection, actions) = Track("a", "b", "c");

        ObservableCollectionReconciler.Reconcile(collection, ["a", "b", "c"]);

        Assert.Equal(["a", "b", "c"], collection);
        Assert.Empty(actions);
    }

    [Fact]
    public void Reconcile_WithInsertionsAndRemovals_KeepsOrderWithoutReset()
    {
        var (collection, actions) = Track("a", "c", "e");

        ObservableCollectionReconciler.Reconcile(collection, ["a", "b", "c", "d"]);

        Assert.Equal(["a", "b", "c", "d"], collection);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
        Assert.Contains(NotifyCollectionChangedAction.Remove, actions);
        Assert.Contains(NotifyCollectionChangedAction.Add, actions);
    }

    [Fact]
    public void Reconcile_WithOnlyRemovals_DoesNotReset()
    {
        var (collection, actions) = Track("a", "b", "c");

        ObservableCollectionReconciler.Reconcile(collection, ["a", "c"]);

        Assert.Equal(["a", "c"], collection);
        Assert.All(actions, x => Assert.Equal(NotifyCollectionChangedAction.Remove, x));
    }

    [Fact]
    public void Reconcile_WithReorder_FallsBackToReset()
    {
        var (collection, actions) = Track("a", "b", "c");

        ObservableCollectionReconciler.Reconcile(collection, ["c", "b", "a"]);

        Assert.Equal(["c", "b", "a"], collection);
        Assert.Contains(NotifyCollectionChangedAction.Reset, actions);
    }

    [Fact]
    public void Reconcile_WithDuplicateDesiredItems_FallsBackToResetAndStaysCorrect()
    {
        var (collection, actions) = Track();

        ObservableCollectionReconciler.Reconcile(collection, ["a", "b", "a"]);
        Assert.Equal(["a", "b", "a"], collection);

        actions.Clear();

        // A second identical pass must not corrupt the content.
        ObservableCollectionReconciler.Reconcile(collection, ["a", "b", "a"]);
        Assert.Equal(["a", "b", "a"], collection);
    }

    [Fact]
    public void Reconcile_WithEmptyTarget_AddsEverything()
    {
        var collection = new ObservableCollection<string>();

        ObservableCollectionReconciler.Reconcile(collection, ["a", "b"]);

        Assert.Equal(["a", "b"], collection);
    }

    [Fact]
    public void Reconcile_WithEmptyDesired_ClearsCollection()
    {
        var collection = new ObservableCollection<string> { "a", "b" };

        ObservableCollectionReconciler.Reconcile(collection, []);

        Assert.Empty(collection);
    }
}
