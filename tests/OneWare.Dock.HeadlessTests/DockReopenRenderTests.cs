using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Xunit;

namespace OneWareDockHeadlessTests;

/// <summary>
/// Headless regression test for issue #258: after closing all tools in a base
/// tool dock the pane is removed from the layout, and reopening a tool must make
/// it render again. This exercises the real Avalonia <see cref="DockControl"/> and
/// mirrors the reopen logic in <c>MainDockService</c>.
///
/// The original bug: reopening rebuilt the docks with raw
/// <c>VisibleDockables.Insert/Add</c> calls, which left <c>IsEmpty</c> stale and
/// the proportions at 0, so the recreated pane rendered with zero size. The fix
/// uses the factory's <c>AddDockable</c>/<c>InsertDockable</c> and resets sibling
/// proportions.
/// </summary>
public class DockReopenRenderTests
{
    private sealed class TestFactory : Factory;

    private static (Window window, RootDock root, TestFactory factory) CreateHosted()
    {
        var factory = new TestFactory();

        var leftTool = new ToolDock
        {
            Id = "LeftPaneTop", Title = "LeftPaneTop", Alignment = Alignment.Left,
            VisibleDockables = factory.CreateList<IDockable>(
                new Tool { Id = "Tool1", Title = "Tool1" },
                new Tool { Id = "Tool2", Title = "Tool2" })
        };

        var left = new ProportionalDock
        {
            Id = "LeftPane", Title = "LeftPane", Proportion = 0.25,
            Orientation = Orientation.Vertical,
            VisibleDockables = factory.CreateList<IDockable>(leftTool)
        };

        var bottomTool = new ToolDock
        {
            Id = "BottomPaneOne", Title = "BottomPaneOne", Alignment = Alignment.Bottom,
            VisibleDockables = factory.CreateList<IDockable>(
                new Tool { Id = "BTool1", Title = "BTool1" })
        };

        var bottom = new ProportionalDock
        {
            Id = "BottomRow", Title = "BottomRow", Proportion = 0.3,
            Orientation = Orientation.Horizontal,
            VisibleDockables = factory.CreateList<IDockable>(bottomTool)
        };

        var documentDock = new DocumentDock
        {
            Id = "Documents", Title = "Documents", IsCollapsable = false,
            VisibleDockables = factory.CreateList<IDockable>()
        };

        var right = new ProportionalDock
        {
            Id = "RightPane", Title = "RightPane", Orientation = Orientation.Vertical,
            VisibleDockables = factory.CreateList<IDockable>(
                documentDock, new ProportionalDockSplitter(), bottom)
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout", Title = "MainLayout", Orientation = Orientation.Horizontal,
            VisibleDockables = factory.CreateList<IDockable>(
                left, new ProportionalDockSplitter(), right)
        };

        var root = new RootDock
        {
            Id = "Root", Title = "Root", IsCollapsable = false,
            VisibleDockables = factory.CreateList<IDockable>(mainLayout),
            ActiveDockable = mainLayout, DefaultDockable = mainLayout,
            LeftPinnedDockables = factory.CreateList<IDockable>(),
            RightPinnedDockables = factory.CreateList<IDockable>(),
            TopPinnedDockables = factory.CreateList<IDockable>(),
            BottomPinnedDockables = factory.CreateList<IDockable>()
        };

        factory.InitLayout(root);

        var dockControl = new DockControl { Factory = factory, Layout = root };
        var window = new Window { Width = 1000, Height = 700, Content = dockControl };
        window.Show();
        Pump(window);

        return (window, root, factory);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(1000, 700));
        window.Arrange(new Rect(0, 0, 1000, 700));
        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<T> SearchView<T>(IDockable? layout)
    {
        if (layout is IDock { VisibleDockables: not null } dock)
            foreach (var dockable in dock.VisibleDockables)
            {
                if (dockable is IDock sub)
                    foreach (var bs in SearchView<T>(sub))
                        yield return bs;
                if (dockable is T tx)
                    yield return tx;
            }
    }

    private static bool ToolIsRendered(Window window, string toolTitle)
    {
        Pump(window);
        return window.GetVisualDescendants()
            .OfType<Control>()
            .Any(c => c.DataContext is Tool t && t.Title == toolTitle
                      && c.Bounds is { Width: > 0, Height: > 0 });
    }

    private static void ResetChildProportions(ProportionalDock dock)
    {
        if (dock.VisibleDockables == null) return;
        foreach (var child in dock.VisibleDockables)
            if (child is not IProportionalDockSplitter)
                child.Proportion = double.NaN;
    }

    // Mirrors MainDockService.Show/FindOrCreateToolDock recreation for the left pane.
    private static void ReopenLeft(TestFactory factory, RootDock root, Tool tool)
    {
        var mainLayout = SearchView<ProportionalDock>(root).First(p => p.Id == "MainLayout");
        var toolDock = SearchView<ToolDock>(root).FirstOrDefault(t => t.Id == "LeftPaneTop");
        if (toolDock == null)
        {
            toolDock = new ToolDock
            {
                Id = "LeftPaneTop", Title = "LeftPaneTop", Alignment = Alignment.Left,
                Proportion = double.NaN, VisibleDockables = factory.CreateList<IDockable>()
            };
            var parentDock = SearchView<ProportionalDock>(root).FirstOrDefault(p => p.Id == "LeftPane");
            if (parentDock == null)
            {
                parentDock = new ProportionalDock
                {
                    Id = "LeftPane", Title = "LeftPane", Proportion = 0.25,
                    Orientation = Orientation.Vertical, VisibleDockables = factory.CreateList<IDockable>()
                };
                ResetChildProportions(mainLayout);
                factory.InsertDockable(mainLayout, parentDock, 0);
                if (mainLayout.VisibleDockables!.Count > 1)
                    factory.InsertDockable(mainLayout, new ProportionalDockSplitter(), 1);
            }
            factory.AddDockable(parentDock, toolDock);
        }
        factory.AddDockable(toolDock, tool);
        factory.SetActiveDockable(tool);
    }

    [AvaloniaFact]
    public void Tool_renders_initially()
    {
        var (window, _, _) = CreateHosted();
        Assert.True(ToolIsRendered(window, "Tool1"), "Tool1 should render initially");
    }

    [AvaloniaFact]
    public void Left_tool_can_be_reopened_after_closing_all()
    {
        var (window, root, factory) = CreateHosted();

        var leftTool = SearchView<ToolDock>(root).First(t => t.Id == "LeftPaneTop");
        var t1 = (Tool)leftTool.VisibleDockables![0];
        var t2 = (Tool)leftTool.VisibleDockables![1];

        factory.CloseDockable(t1);
        factory.CloseDockable(t2);
        Pump(window);
        Assert.Null(SearchView<ToolDock>(root).FirstOrDefault(t => t.Id == "LeftPaneTop"));

        ReopenLeft(factory, root, t1);

        Assert.True(ToolIsRendered(window, "Tool1"),
            "Tool1 must render after reopening once all left tools were closed (issue #258)");
    }

    [AvaloniaFact]
    public void Left_tool_can_be_reopened_across_multiple_close_cycles()
    {
        var (window, root, factory) = CreateHosted();

        var leftTool = SearchView<ToolDock>(root).First(t => t.Id == "LeftPaneTop");
        var t1 = (Tool)leftTool.VisibleDockables![0];
        var t2 = (Tool)leftTool.VisibleDockables![1];

        factory.CloseDockable(t1);
        factory.CloseDockable(t2);
        Pump(window);

        ReopenLeft(factory, root, t1);
        Assert.True(ToolIsRendered(window, "Tool1"), "First reopen cycle failed");

        factory.CloseDockable(t1);
        Pump(window);

        ReopenLeft(factory, root, t2);
        Assert.True(ToolIsRendered(window, "Tool2"), "Second reopen cycle failed");
    }
}
