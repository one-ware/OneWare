using System.Collections.ObjectModel;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace OneWare.Core.ViewModels.DockViews;

public class MainDocumentDockViewModel : DocumentDock
{
    public MainDocumentDockViewModel()
    {
        Id = "CentralDocumentDock";
        IsCollapsable = false;

        VisibleDockables = new ObservableCollection<IDockable>();

        LayoutMode = DocumentLayoutMode.Tabbed;
    }

    public new bool IsEmpty => false;
}