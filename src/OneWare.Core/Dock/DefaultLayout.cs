using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Essentials.Enums;

namespace OneWare.Core.Dock;

public class DefaultLayout
{
    private readonly DockService _dockService;
    private readonly IFactory _factory;
    private readonly MainDocumentDockViewModel _mainDocumentDock;
    private readonly Func<Type, IDockable> _dockableResolver;

    public DefaultLayout(
        DockService dockService,
        IFactory factory,
        MainDocumentDockViewModel mainDocumentDock,
        Func<Type, IDockable> dockableResolver)
    {
        _dockService = dockService;
        _factory = factory;
        _mainDocumentDock = mainDocumentDock;
        _dockableResolver = dockableResolver;
    }

    private IList<IDockable> ConvertRegistration(IEnumerable<Type>? types)
    {
        if (types == null)
            return _factory.CreateList<IDockable>();

        var dockables = types.Select(t => _dockableResolver(t)).ToArray();
        return _factory.CreateList(dockables);
    }

    public RootDock GetDefaultLayout()
    {
        _mainDocumentDock.ActiveDockable = null;
        _mainDocumentDock.FocusedDockable = null;
        _mainDocumentDock.VisibleDockables?.Clear();

        _dockService.LayoutRegistrations.TryGetValue(DockShowLocation.Left, out var leftTools);
        _dockService.LayoutRegistrations.TryGetValue(DockShowLocation.Bottom, out var bottomTools);

        var leftTool = new ToolDock
        {
            Id = "LeftPaneTop",
            Title = "LeftPaneTop",
            VisibleDockables = ConvertRegistration(leftTools),
            Alignment = Alignment.Left
        };

        var left = new ProportionalDock
        {
            Id = "LeftPane",
            Title = "LeftPane",
            Proportion = 0.25,
            Orientation = Orientation.Vertical,
            VisibleDockables = _dockService.CreateList<IDockable>(leftTool)
        };

        var bottomTool = new ToolDock
        {
            Id = "BottomPaneOne",
            Title = "BottomPaneOne",
            VisibleDockables = ConvertRegistration(bottomTools),
            Alignment = Alignment.Bottom
        };

        var bottom = new ProportionalDock
        {
            Id = "BottomRow",
            Title = "BottomRow",
            Proportion = 0.3,
            Orientation = Orientation.Horizontal,
            VisibleDockables = _dockService.CreateList<IDockable>(bottomTool)
        };

        var right = new ProportionalDock
        {
            Id = "TopRow",
            Title = "TopRow",
            Orientation = Orientation.Vertical,
            ActiveDockable = null,
            VisibleDockables = _dockService.CreateList<IDockable>(
                _mainDocumentDock,
                new ProportionalDockSplitter(),
                bottom)
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Title = "MainLayout",
            Orientation = Orientation.Horizontal,
            ActiveDockable = null,
            VisibleDockables = _dockService.CreateList<IDockable>(
                left,
                new ProportionalDockSplitter(),
                right)
        };

        var root = new RootDock
        {
            Id = "Root",
            Title = "Root",
            IsCollapsable = false,
            VisibleDockables = _dockService.CreateList<IDockable>(mainLayout),
            ActiveDockable = mainLayout,
            DefaultDockable = mainLayout
        };

        return root;
    }
}
