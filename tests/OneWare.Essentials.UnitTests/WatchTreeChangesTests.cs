using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public class WatchTreeChangesTests
{
    private readonly List<(string Id, string Path)> _added = [];
    private readonly List<(string Id, string Path)> _removed = [];

    private IDisposable Watch(ObservableCollection<MenuItemModel> root)
    {
        return root.WatchTreeChanges((i, p) => _added.Add((i.PartId, p)), (i, p) => _removed.Add((i.PartId, p)));
    }

    [Fact]
    public void ReportsExistingItemsWithTheirParentPath()
    {
        var view = new MenuItemModel("View") { Header = "View", Items = [new MenuItemModel("Reset")] };
        using var _ = Watch([view]);

        Assert.Equal([("View", ""), ("Reset", "View \u2192 ")], _added);
    }

    [Fact]
    public void ReportsItemsAddedToNestedCollections()
    {
        var toolWindows = new MenuItemModel("ToolWindows") { Header = "Tool Windows", Items = [] };
        var view = new MenuItemModel("View") { Header = "View", Items = [toolWindows] };
        using var _ = Watch([view]);
        _added.Clear();

        toolWindows.Items!.Add(new MenuItemModel("Chat"));

        Assert.Equal([("Chat", "View \u2192 Tool Windows \u2192 ")], _added);
    }

    [Fact]
    public void ReportsItemsWhenExistingItemGetsAnItemsCollection()
    {
        var view = new MenuItemModel("View") { Header = "View" };
        using var _ = Watch([view]);
        _added.Clear();

        view.Items = [new MenuItemModel("Reset")];
        view.Items.Add(new MenuItemModel("Chat"));

        Assert.Equal([("Reset", "View \u2192 "), ("Chat", "View \u2192 ")], _added);
    }

    [Fact]
    public void StopsWatchingReplacedItemsCollection()
    {
        var oldItems = new ObservableCollection<MenuItemModel> { new("Reset") };
        var view = new MenuItemModel("View") { Header = "View", Items = oldItems };
        using var _ = Watch([view]);
        _added.Clear();

        view.Items = [];
        oldItems.Add(new MenuItemModel("Stale"));

        Assert.Equal([("Reset", "View \u2192 ")], _removed);
        Assert.Empty(_added);
    }

    [Fact]
    public void ReportsRemovalOfItemAndDescendants()
    {
        var children = new ObservableCollection<MenuItemModel> { new("Reset") };
        var view = new MenuItemModel("View") { Header = "View", Items = children };
        var root = new ObservableCollection<MenuItemModel> { view };
        using var _ = Watch(root);
        _added.Clear();

        root.Remove(view);
        children.Add(new MenuItemModel("Stale"));

        Assert.Equal([("View", ""), ("Reset", "View \u2192 ")], _removed);
        Assert.Empty(_added);
    }

    [Fact]
    public void DisposeStopsWatching()
    {
        var children = new ObservableCollection<MenuItemModel>();
        var root = new ObservableCollection<MenuItemModel> { new("View") { Header = "View", Items = children } };
        Watch(root).Dispose();
        _added.Clear();

        root.Add(new MenuItemModel("Help"));
        children.Add(new MenuItemModel("Reset"));

        Assert.Empty(_added);
    }
}
