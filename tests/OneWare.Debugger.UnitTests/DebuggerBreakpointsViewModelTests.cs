using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.EditorExtensions;
using Xunit;

namespace OneWare.Debugger.UnitTests;

/// <summary>
///     Deckt das Entfernen von Breakpoints aus dem Breakpoints-Reiter ab. Zwei Dinge sind hier
///     nicht verhandelbar: es darf nur die Auswahl geloescht werden, und der Store muss jeden
///     Eintrag einzeln als Remove melden - die laufende Session haengt an diesen Events, um die
///     Breakpoints auch am Ziel abzuraeumen.
///     <br /><br />
///     <see cref="BreakpointStore" /> ist ein statisches Singleton, deshalb raeumt die Klasse vor
///     und nach jedem Test auf. xUnit fuehrt Tests innerhalb einer Klasse sequenziell aus, die
///     uebrigen Testklassen der Assembly fassen den Store nicht an.
/// </summary>
public class DebuggerBreakpointsViewModelTests : IDisposable
{
    private static readonly BreakpointStore Store = BreakpointStore.Instance;

    public DebuggerBreakpointsViewModelTests()
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
        var vm = new DebuggerBreakpointsViewModel();
        var first = Add(@"C:\proj\src\top.vhd", 10);
        var second = Add(@"C:\proj\src\top.vhd", 20);
        var third = Add(@"C:\proj\src\uart.vhd", 5);

        // Nicht benachbarte Auswahl, wie bei Ctrl-Klick auf Zeile 1 und 3.
        vm.SelectedBreakpoints.Add(first);
        vm.SelectedBreakpoints.Add(third);

        vm.RemoveBreakpointCommand.Execute(null);

        Assert.Equal([second], vm.Breakpoints);
    }

    [Fact]
    public void RemoveBreakpointReportsEveryEntryIndividually()
    {
        var vm = new DebuggerBreakpointsViewModel();
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

        // Ohne diese Einzelmeldungen wuesste die Session nicht, welche Breakpoints sie in GDB
        // loeschen soll - ein Clear() auf dem Store wuerde nur ein Reset ohne OldItems feuern.
        Assert.Equal([first, third], removed);
        Assert.Equal(0, resets);
    }

    [Fact]
    public void RemoveBreakpointIsOnlyExecutableWithASelection()
    {
        var vm = new DebuggerBreakpointsViewModel();
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
        var vm = new DebuggerBreakpointsViewModel();

        Assert.False(vm.RemoveAllBreakpointsCommand.CanExecute(null));

        Add(@"C:\proj\src\top.vhd", 10);
        Assert.True(vm.RemoveAllBreakpointsCommand.CanExecute(null));

        vm.RemoveAllBreakpointsCommand.Execute(null);
        Assert.False(vm.RemoveAllBreakpointsCommand.CanExecute(null));
    }

    [Fact]
    public void RemoveAllBreakpointsClearsStoreAndSelection()
    {
        var vm = new DebuggerBreakpointsViewModel();
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
