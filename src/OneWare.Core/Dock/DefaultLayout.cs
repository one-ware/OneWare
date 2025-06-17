using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Essentials.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OneWare.Core.Dock
{
    public class DefaultLayout
    {
        private readonly MainDocumentDockViewModel _mainDocumentDockViewModel;

        public DefaultLayout(MainDocumentDockViewModel mainDocumentDockViewModel)
        {
            _mainDocumentDockViewModel = mainDocumentDockViewModel;
        }

        private static IList<IDockable> ConvertRegistration(
            IEnumerable<Type>? types,
            DockService dockService,
            IFactory factory)
        {
            var dockables = types?
                .Select(type => factory.GetDockable<IDockable>(type.FullName))
                .Where(d => d != null)
                .Cast<IDockable>()
                .ToArray();

            return dockables == null
                ? factory.CreateList<IDockable>()
                : factory.CreateList(dockables);
        }

        public RootDock GetDefaultLayout(DockService dockService)
        {
            _mainDocumentDockViewModel.ActiveDockable = null;
            _mainDocumentDockViewModel.FocusedDockable = null;
            _mainDocumentDockViewModel.VisibleDockables?.Clear();

            dockService.LayoutRegistrations.TryGetValue(DockShowLocation.Left, out var leftTools);
            dockService.LayoutRegistrations.TryGetValue(DockShowLocation.Bottom, out var bottomTools);

            var leftTool = new ToolDock
            {
                Id = "LeftPaneTop",
                Title = "LeftPaneTop",
                VisibleDockables = ConvertRegistration(leftTools, dockService, dockService),
                Alignment = Alignment.Left
            };

            var left = new ProportionalDock
            {
                Id = "LeftPane",
                Title = "LeftPane",
                Proportion = 0.25,
                Orientation = Orientation.Vertical,
                VisibleDockables = dockService.CreateList<IDockable>(leftTool)
            };

            var bottomTool = new ToolDock
            {
                Id = "BottomPaneOne",
                Title = "BottomPaneOne",
                VisibleDockables = ConvertRegistration(bottomTools, dockService, dockService),
                Alignment = Alignment.Bottom
            };

            var bottom = new ProportionalDock
            {
                Id = "BottomRow",
                Title = "BottomRow",
                Proportion = 0.3,
                Orientation = Orientation.Horizontal,
                VisibleDockables = dockService.CreateList<IDockable>(bottomTool)
            };

            var right = new ProportionalDock
            {
                Id = "TopRow",
                Title = "TopRow",
                Orientation = Orientation.Vertical,
                ActiveDockable = null,
                VisibleDockables = dockService.CreateList<IDockable>(
                    _mainDocumentDockViewModel,
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
                VisibleDockables = dockService.CreateList<IDockable>(
                    left,
                    new ProportionalDockSplitter(),
                    right
                )
            };

            var root = new RootDock
            {
                Id = "Root",
                Title = "Root",
                IsCollapsable = false,
                VisibleDockables = dockService.CreateList<IDockable>(mainLayout),
                ActiveDockable = mainLayout,
                DefaultDockable = mainLayout
            };

            return root;
        }
    }
}
