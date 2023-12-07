using System.Collections.ObjectModel;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using DynamicData.Binding;

namespace OneWare.Core.ViewModels.DockViews;

public class MainDocumentDockViewModel : DocumentDock
{
    public new bool IsEmpty => false;
    
    public MainDocumentDockViewModel()
    {
        Id = "CentralDocumentDock";
        IsCollapsable = false;

        VisibleDockables = new ObservableCollection<IDockable>();
    }
}