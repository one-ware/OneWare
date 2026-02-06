using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;

namespace OneWare.Core.Dock;

public static class DefaultLayout
{
    private static IList<IDockable> ConvertRegistration(IEnumerable<Type>? types, IFactory factory, double proportion = double.NaN)
    {
        var toolsResolved = types?
            .Select(x => ContainerLocator.Container.Resolve(x))
            .Cast<IDockable>();
        
        return toolsResolved == null
            ? factory.CreateList<IDockable>()
            : factory.CreateList(toolsResolved.Select(x =>
            {
                x.Proportion = proportion;
                x.PinnedBounds = null;
                return x;
            }).ToArray());
    }

    public static RootDock GetDefaultLayout(MainDockService mainDockService)
    {
        var documentDock = ContainerLocator.Container.Resolve<MainDocumentDockViewModel>();
        documentDock.ActiveDockable = null;
        documentDock.FocusedDockable = null;
        documentDock.VisibleDockables?.Clear();
        mainDockService.LayoutRegistrations.TryGetValue(DockShowLocation.Left, out var leftTools);
        mainDockService.LayoutRegistrations.TryGetValue(DockShowLocation.Bottom, out var bottomTools);

        var leftTool = new ToolDock
        {
            Id = "LeftPaneTop",
            Title = "LeftPaneTop",
            VisibleDockables = ConvertRegistration(leftTools, mainDockService),
            Alignment = Alignment.Left,
        };

        var left = new ProportionalDock
        {
            Id = "LeftPane",
            Title = "LeftPane",
            Proportion = 0.25,
            Orientation = Orientation.Vertical,
            VisibleDockables = mainDockService.CreateList<IDockable>
            (
                leftTool
            ),
        };

        var bottomTool = new ToolDock
        {
            Id = "BottomPaneOne",
            Title = "BottomPaneOne",
            VisibleDockables = ConvertRegistration(bottomTools, mainDockService),
            Alignment = Alignment.Bottom
        };

        var bottom = new ProportionalDock
        {
            Id = "BottomRow",
            Title = "BottomRow",
            Proportion = 0.3,
            Orientation = Orientation.Horizontal,
            VisibleDockables = mainDockService.CreateList<IDockable>
            (
                bottomTool
            )
        };

        var right = new ProportionalDock
        {
            Id = "RightPane",
            Title = "TopRow",
            Orientation = Orientation.Vertical,
            ActiveDockable = null,
            VisibleDockables = mainDockService.CreateList<IDockable>
            (
                documentDock,
                new ProportionalDockSplitter(),
                bottom
            )
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Title = "MainLayout",
            Orientation = Orientation.Horizontal,
            ActiveDockable = null,
            VisibleDockables = mainDockService.CreateList<IDockable>
            (
                left,
                new ProportionalDockSplitter(),
                right
            ),
        };
        
        mainDockService.LayoutRegistrations.TryGetValue(DockShowLocation.LeftPinned, out var leftPinned);
        mainDockService.LayoutRegistrations.TryGetValue(DockShowLocation.RightPinned, out var rightPinned);

        var root = new RootDock
        {
            Id = "Root",
            Title = "Root",
            IsCollapsable = false,
            VisibleDockables = mainDockService.CreateList<IDockable>(mainLayout),
            ActiveDockable = mainLayout,
            DefaultDockable = mainLayout,
            PinnedDockDisplayMode = PinnedDockDisplayMode.Inline,
            RightPinnedDockables = ConvertRegistration(rightPinned, mainDockService, 0.3),
            LeftPinnedDockables = ConvertRegistration(leftPinned, mainDockService, 0.3),
            BottomPinnedDockables = mainDockService.CreateList<IDockable>(),
            TopPinnedDockables = mainDockService.CreateList<IDockable>()
        };

        return root;
    }
}