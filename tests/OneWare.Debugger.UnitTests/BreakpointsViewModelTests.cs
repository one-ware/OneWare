using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using OneWare.Debugger.ViewModels.Inspector;
using OneWare.Essentials.EditorExtensions;
using Xunit;

namespace OneWare.Debugger.UnitTests;

/// <summary>
///     Covers removing breakpoints from the Breakpoints tab. Two things are
///     non-negotiable here: only the selection may be deleted, and the store must report every
///     entry individually as a Remove - the running session listens to these events to
///     clear the breakpoints on the target as well.
///     <br /><br />
///     <see cref="BreakpointStore" /> is a static singleton, so the class cleans up before
///     and after every test. xUnit runs tests within one class sequentially, and the
///     other test classes of the assembly do not touch the store.
/// </summary>
public class BreakpointsViewModelTests : IDisposable
{
    private static readonly BreakpointStore Store = BreakpointStore.Instance;

    public BreakpointsViewModelTests()
    {
        Store.Breakpoints.Clear();
    }

    public void Dispose()
    {
        Store.Breakpoints.Clear();
    }

    private static BreakPoint Add(string file, int line)
    {
        var breakpoint = new BreakPoint { File = file, Line = line };
        Store.Add(breakpoint);
        return breakpoint;
    }

    [Fact]
    public void RemoveBreakpointRemovesOnlyTheSelectedEntries()
    {
        var vm = new BreakpointsViewModel();
        var first = Add(@"C:\proj\src\top.vhd", 10);
        var second = Add(@"C:\proj\src\top.vhd", 20);
        var third = Add(@"C:\proj\src\uart.vhd", 5);

        // Non-adjacent selection, as with Ctrl-click on rows 1 and 3.
        vm.SelectedBreakpoints.Add(first);
        vm.SelectedBreakpoints.Add(third);

        vm.RemoveBreakpointCommand.Execute(null);

        Assert.Equal([second], vm.Breakpoints.Cast<BreakPoint>());
    }

    [Fact]
    public void BreakpointsAreSortedByFileThenLine()
    {
        var vm = new BreakpointsViewModel();

        // Deliberately entered in the wrong order: the view sorts, not the store.
        var uart = Add(@"C:\proj\src\uart.vhd", 5);
        var topTen = Add(@"C:\proj\src\top.vhd", 10);
        var topNine = Add(@"C:\proj\src\top.vhd", 9);

        // Line 9 before line 10: Line is an int and is sorted numerically. As text
        // "10" would have ended up before "9".
        Assert.Equal([topNine, topTen, uart], vm.Breakpoints.Cast<BreakPoint>());
    }

    [Fact]
    public void RemoveBreakpointReportsEveryEntryIndividually()
    {
        var vm = new BreakpointsViewModel();
        var first = Add(@"C:\proj\src\top.vhd", 10);
        Add(@"C:\proj\src\top.vhd", 20);
        var third = Add(@"C:\proj\src\uart.vhd", 5);

        vm.SelectedBreakpoints.Add(first);
        vm.SelectedBreakpoints.Add(third);

        var removed = new List<BreakPoint>();
        var resets = 0;
        Store.Breakpoints.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) resets++;
            if (e.OldItems is not null) removed.AddRange(e.OldItems.Cast<BreakPoint>());
        };

        vm.RemoveBreakpointCommand.Execute(null);

        // Without these individual notifications the session would not know which breakpoints to
        // delete in GDB - a Clear() on the store would only fire a Reset without OldItems.
        Assert.Equal([first, third], removed);
        Assert.Equal(0, resets);
    }

    [Fact]
    public void RemoveBreakpointIsOnlyExecutableWithASelection()
    {
        var vm = new BreakpointsViewModel();
        var breakpoint = Add(@"C:\proj\src\top.vhd", 10);

        Assert.False(vm.RemoveBreakpointCommand.CanExecute(null));

        vm.SelectedBreakpoints.Add(breakpoint);
        Assert.True(vm.RemoveBreakpointCommand.CanExecute(null));

        vm.SelectedBreakpoints.Clear();
        Assert.False(vm.RemoveBreakpointCommand.CanExecute(null));
    }

    [Fact]
    public void RemoveAllBreakpointsIsOnlyExecutableWhenTheListHasEntries()
    {
        var vm = new BreakpointsViewModel();

        Assert.False(vm.RemoveAllBreakpointsCommand.CanExecute(null));

        Add(@"C:\proj\src\top.vhd", 10);
        Assert.True(vm.RemoveAllBreakpointsCommand.CanExecute(null));

        vm.RemoveAllBreakpointsCommand.Execute(null);
        Assert.False(vm.RemoveAllBreakpointsCommand.CanExecute(null));
    }

    [Fact]
    public void RemoveAllBreakpointsClearsStoreAndSelection()
    {
        var vm = new BreakpointsViewModel();
        var first = Add(@"C:\proj\src\top.vhd", 10);
        Add(@"C:\proj\src\uart.vhd", 5);
        vm.SelectedBreakpoints.Add(first);

        var resets = 0;
        Store.Breakpoints.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) resets++;
        };

        vm.RemoveAllBreakpointsCommand.Execute(null);

        Assert.Empty(vm.Breakpoints);
        Assert.Empty(vm.SelectedBreakpoints);
        Assert.Equal(0, resets);
    }
}
