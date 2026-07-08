using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Model.Mvvm.Core;
using Xunit;

namespace OneWareDockHeadlessTests;

/// <summary>
/// Regression tests for a corrupt persisted layout where the main root carries a
/// stray <see cref="RootDock.Window"/> whose inner root shares (duplicates) the
/// same MainLayout instance that is also in the main root's VisibleDockables.
///
/// The main layout is always hosted in the MainWindow, so the persisted root
/// should never have a Window (real floating windows live in the Windows list).
/// <c>MainDockService.RemoveStrayRootWindow</c> detaches it on load. These tests
/// mirror that repair and verify the tree becomes clean and still renders.
/// </summary>
public class CorruptWindowLayoutTests
{
    private sealed class TestFactory : Factory;

    private static (RootDock outerRoot, RootDock innerRoot, ProportionalDock mainLayout) BuildCorrupt(TestFactory factory)
    {
        var tool = new Tool { Id = "Explorer", Title = "Explorer" };
        var toolDock = new ToolDock
        {
            Id = "LeftPaneTop", Title = "LeftPaneTop", Alignment = Alignment.Left,
            VisibleDockables = factory.CreateList<IDockable>(tool)
        };
        var left = new ProportionalDock
        {
            Id = "LeftPane", Title = "LeftPane", Proportion = 0.25,
            Orientation = Orientation.Vertical,
            VisibleDockables = factory.CreateList<IDockable>(toolDock)
        };
        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout", Title = "MainLayout", Orientation = Orientation.Horizontal,
            VisibleDockables = factory.CreateList<IDockable>(left)
        };

        // Inner root shares the SAME mainLayout instance (as PreserveReferences produces).
        var innerRoot = new RootDock
        {
            Id = "", Title = "IRootDock",
            VisibleDockables = factory.CreateList<IDockable>(mainLayout),
            ActiveDockable = mainLayout, DefaultDockable = mainLayout,
            LeftPinnedDockables = factory.CreateList<IDockable>(),
            RightPinnedDockables = factory.CreateList<IDockable>(),
            TopPinnedDockables = factory.CreateList<IDockable>(),
            BottomPinnedDockables = factory.CreateList<IDockable>()
        };
        var window = new DockWindow { Id = "IDockWindow", Layout = innerRoot };
        innerRoot.Window = window;

        var outerRoot = new RootDock
        {
            Id = "Default", Title = "Root", IsCollapsable = false,
            Window = window, // stray window on the main root - the corruption
            VisibleDockables = factory.CreateList<IDockable>(mainLayout),
            ActiveDockable = mainLayout, DefaultDockable = mainLayout,
            LeftPinnedDockables = factory.CreateList<IDockable>(),
            RightPinnedDockables = factory.CreateList<IDockable>(),
            TopPinnedDockables = factory.CreateList<IDockable>(),
            BottomPinnedDockables = factory.CreateList<IDockable>(),
            Windows = factory.CreateList<IDockWindow>()
        };

        return (outerRoot, innerRoot, mainLayout);
    }

    // Mirrors MainDockService.RemoveStrayRootWindow.
    private static void RemoveStrayRootWindow(RootDock root)
    {
        if (root.Window == null) return;
        if (root.Window.Layout is { } strayLayout)
            strayLayout.Window = null;
        root.Window.Layout = null;
        root.Window = null;
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(1000, 700));
        window.Arrange(new Rect(0, 0, 1000, 700));
        Dispatcher.UIThread.RunJobs();
    }

    private static bool ToolIsRendered(Window window, string toolTitle)
    {
        Pump(window);
        return window.GetVisualDescendants()
            .OfType<Control>()
            .Any(c => c.DataContext is Tool t && t.Title == toolTitle
                      && c.Bounds is { Width: > 0, Height: > 0 });
    }

    [AvaloniaFact]
    public void Repair_detaches_stray_window_and_breaks_duplicate_reference()
    {
        var factory = new TestFactory();
        var (outerRoot, innerRoot, mainLayout) = BuildCorrupt(factory);

        RemoveStrayRootWindow(outerRoot);

        Assert.Null(outerRoot.Window);
        Assert.Null(innerRoot.Window);
        // MainLayout is now reachable only from the main root.
        Assert.Contains(mainLayout, outerRoot.VisibleDockables!);
    }

    [AvaloniaFact]
    public void Repaired_layout_initializes_and_renders()
    {
        var factory = new TestFactory();
        var (outerRoot, _, mainLayout) = BuildCorrupt(factory);

        RemoveStrayRootWindow(outerRoot);
        factory.InitLayout(outerRoot);

        Assert.Same(outerRoot, mainLayout.Owner);

        var dockControl = new DockControl { Factory = factory, Layout = outerRoot };
        var window = new Window { Width = 1000, Height = 700, Content = dockControl };
        window.Show();

        Assert.True(ToolIsRendered(window, "Explorer"),
            "Explorer must render after repairing the corrupt layout");
    }
}
