using System;
using System.Collections.Generic;
using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using Prism.Ioc;
using OneWare.Shared.Enums;

namespace OneWare.Core.Dock
{
    public static class DefaultLayout
    {
        private static IList<IDockable> ConvertRegistration(IEnumerable<Type>? types, IFactory factory)
        {
            var bottomToolsResolved = types?
                .Select(x => ContainerLocator.Container.Resolve(x))
                .Cast<IDockable>();

            return bottomToolsResolved == null ? factory.CreateList<IDockable>() : factory.CreateList(bottomToolsResolved.ToArray());
        }
        
        public static IRootDock GetDefaultLayout(DockService dockService)
        {
            var documentDock = ContainerLocator.Container.Resolve<MainDocumentDockViewModel>();
            documentDock.ActiveDockable = null;
            documentDock.FocusedDockable = null;
            documentDock.VisibleDockables?.Clear();
            dockService.LayoutRegistrations.TryGetValue(DockShowLocation.Left, out var leftTools);
            dockService.LayoutRegistrations.TryGetValue(DockShowLocation.Bottom, out var bottomTools);

            var leftTool = new ToolDock
            {
                Id = "LeftPaneTop",
                Title = "LeftPaneTop",
                VisibleDockables = ConvertRegistration(leftTools, dockService),
                Alignment = Alignment.Left
            };
        
            var left = new ProportionalDock
            {
                Id = "LeftPane",
                Title = "LeftPane",
                Proportion = 0.25,
                Orientation = Orientation.Vertical,
                VisibleDockables = dockService.CreateList<IDockable>
                (
                    leftTool
                )
            };
        
            var bottomTool = new ToolDock
            {
                Id = "BottomPaneOne",
                Title = "BottomPaneOne",
                VisibleDockables = ConvertRegistration(bottomTools, dockService),
                Alignment = Alignment.Bottom
            };
        
            var bottom = new ProportionalDock
            {
                Id = "BottomRow",
                Title = "BottomRow",
                Proportion = 0.3,
                Orientation = Orientation.Horizontal,
                VisibleDockables = dockService.CreateList<IDockable>
                (
                    bottomTool
                )
            };
        
            var right = new ProportionalDock
            {
                Id = "TopRow",
                Title = "TopRow",
                Orientation = Orientation.Vertical,
                ActiveDockable = null,
                VisibleDockables = dockService.CreateList<IDockable>
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
                VisibleDockables = dockService.CreateList<IDockable>
                (
                    left,
                    new ProportionalDockSplitter(),
                    right
                )
            };
        
            var root = dockService.CreateRootDock();
        
            root.Id = "Root";
            root.Title = "Root";
            root.IsCollapsable = false;
        
            root.VisibleDockables = dockService.CreateList<IDockable>(mainLayout);
            root.ActiveDockable = mainLayout;
            root.DefaultDockable = mainLayout;
        
            return root;
        }
    }
}