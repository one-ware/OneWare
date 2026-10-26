using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Linq;
using OneWare.Debugger.ViewModels.Inspector;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
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
    private static readonly string ProjectRoot = Path.Combine(Path.GetTempPath(), "proj");
    private static readonly string Src = Path.Combine(ProjectRoot, "src") + Path.DirectorySeparatorChar;
    private static readonly string OtherSrc = Path.Combine(Path.GetTempPath(), "proj2", "src") + Path.DirectorySeparatorChar;

    private readonly IProjectExplorerService _explorer = FakeObject.Create<IProjectExplorerService>();

    public BreakpointsViewModelTests()
    {
        Store.Breakpoints.Clear();
    }

    public void Dispose()
    {
        Store.Breakpoints.Clear();
    }

    private BreakpointsViewModel CreateViewModel()
    {
        _explorer.ActiveProject = Project(ProjectRoot);
        return new BreakpointsViewModel(_explorer);
    }

    private static IProjectRoot Project(string root)
    {
        var project = FakeObject.Create<IProjectRoot>();
        ((FakeObject)(object)project).Values["RootFolderPath"] = root;
        return project;
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
        var vm = CreateViewModel();
        var first = Add(Src + @"top.vhd", 10);
        var second = Add(Src + @"top.vhd", 20);
        var third = Add(Src + @"uart.vhd", 5);

        // Non-adjacent selection, as with Ctrl-click on rows 1 and 3.
        vm.SelectedBreakpoints.Add(first);
        vm.SelectedBreakpoints.Add(third);

        vm.RemoveBreakpointCommand.Execute(null);

        Assert.Equal([second], vm.Breakpoints.Cast<BreakPoint>());
    }

    [Fact]
    public void BreakpointsAreSortedByFileThenLine()
    {
        var vm = CreateViewModel();

        // Deliberately entered in the wrong order: the view sorts, not the store.
        var uart = Add(Src + @"uart.vhd", 5);
        var topTen = Add(Src + @"top.vhd", 10);
        var topNine = Add(Src + @"top.vhd", 9);

        // Line 9 before line 10: Line is an int and is sorted numerically. As text
        // "10" would have ended up before "9".
        Assert.Equal([topNine, topTen, uart], vm.Breakpoints.Cast<BreakPoint>());
    }

    [Fact]
    public void RemoveBreakpointReportsEveryEntryIndividually()
    {
        var vm = CreateViewModel();
        var first = Add(Src + @"top.vhd", 10);
        Add(Src + @"top.vhd", 20);
        var third = Add(Src + @"uart.vhd", 5);

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
        var vm = CreateViewModel();
        var breakpoint = Add(Src + @"top.vhd", 10);

        Assert.False(vm.RemoveBreakpointCommand.CanExecute(null));

        vm.SelectedBreakpoints.Add(breakpoint);
        Assert.True(vm.RemoveBreakpointCommand.CanExecute(null));

        vm.SelectedBreakpoints.Clear();
        Assert.False(vm.RemoveBreakpointCommand.CanExecute(null));
    }

    [Fact]
    public void RemoveAllBreakpointsIsOnlyExecutableWhenTheListHasEntries()
    {
        var vm = CreateViewModel();

        Assert.False(vm.RemoveAllBreakpointsCommand.CanExecute(null));

        Add(Src + @"top.vhd", 10);
        Assert.True(vm.RemoveAllBreakpointsCommand.CanExecute(null));

        vm.RemoveAllBreakpointsCommand.Execute(null);
        Assert.False(vm.RemoveAllBreakpointsCommand.CanExecute(null));
    }

    [Fact]
    public void RemoveAllBreakpointsClearsStoreAndSelection()
    {
        var vm = CreateViewModel();
        var first = Add(Src + @"top.vhd", 10);
        Add(Src + @"uart.vhd", 5);
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

    [Fact]
    public void OnlyBreakpointsOfTheActiveProjectAreListed()
    {
        var vm = CreateViewModel();
        var own = Add(Src + "top.vhd", 10);
        var foreign = Add(OtherSrc + "top.vhd", 10);

        Assert.Equal([own], vm.Breakpoints.Cast<BreakPoint>());

        _explorer.ActiveProject = Project(Path.Combine(Path.GetTempPath(), "proj2"));
        Assert.Equal([foreign], vm.Breakpoints.Cast<BreakPoint>());

        _explorer.ActiveProject = null;
        Assert.Empty(vm.Breakpoints);
        Assert.False(vm.HasBreakpoints);
    }

    [Fact]
    public void RemoveAllBreakpointsKeepsOtherProjects()
    {
        var vm = CreateViewModel();
        Add(Src + "top.vhd", 10);
        var foreign = Add(OtherSrc + "top.vhd", 10);

        vm.RemoveAllBreakpointsCommand.Execute(null);

        Assert.Equal([foreign], Store.Breakpoints);
    }

    [Fact]
    public void RemovingAHiddenBreakpointDoesNotDisturbTheList()
    {
        var vm = CreateViewModel();
        var own = Add(Src + "top.vhd", 10);
        var foreign = Add(OtherSrc + "top.vhd", 10);

        Store.Remove(foreign);

        Assert.Equal([own], vm.Breakpoints.Cast<BreakPoint>());
    }

    [Fact]
    public void FilterMatchesPathAndLine()
    {
        var vm = CreateViewModel();
        var top = Add(Src + "top.vhd", 12);
        var uart = Add(Src + "uart.vhd", 5);

        vm.FilterText = "UART";
        Assert.Equal([uart], vm.Breakpoints.Cast<BreakPoint>());

        vm.FilterText = "top.vhd:12";
        Assert.Equal([top], vm.Breakpoints.Cast<BreakPoint>());

        vm.FilterText = "nothing";
        Assert.Empty(vm.Breakpoints);
        Assert.True(vm.HasBreakpoints);
        Assert.True(vm.HasNoMatches);

        // Added while filtered: only listed if it matches.
        vm.FilterText = "uart";
        Add(Src + "top.vhd", 30);
        Assert.Equal([uart], vm.Breakpoints.Cast<BreakPoint>());

        vm.FilterText = string.Empty;
        Assert.Equal(3, vm.Breakpoints.Cast<BreakPoint>().Count());
    }

    // Minimal stand-in for interfaces: auto-properties backed by a dictionary and a working
    // PropertyChanged, everything else returns defaults.
    public class FakeObject : DispatchProxy
    {
        private PropertyChangedEventHandler? _propertyChanged;

        public Dictionary<string, object?> Values { get; } = [];

        public static T Create<T>() => DispatchProxy.Create<T, FakeObject>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var name = targetMethod?.Name ?? string.Empty;

            switch (name)
            {
                case "add_PropertyChanged":
                    _propertyChanged += (PropertyChangedEventHandler?)args![0];
                    return null;
                case "remove_PropertyChanged":
                    _propertyChanged -= (PropertyChangedEventHandler?)args![0];
                    return null;
            }

            if (name.StartsWith("set_", StringComparison.Ordinal))
            {
                Values[name[4..]] = args![0];
                _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(name[4..]));
                return null;
            }

            if (name.StartsWith("get_", StringComparison.Ordinal) && Values.TryGetValue(name[4..], out var value))
                return value;

            var type = targetMethod?.ReturnType;
            return type is { IsValueType: true } && type != typeof(void) ? Activator.CreateInstance(type) : null;
        }
    }
}
