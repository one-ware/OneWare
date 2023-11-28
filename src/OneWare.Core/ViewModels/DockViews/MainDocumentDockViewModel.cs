using System.Collections.ObjectModel;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace OneWare.Core.ViewModels.DockViews;

public class MainDocumentDockViewModel : DocumentDock
{
    public MainDocumentDockViewModel(WelcomeScreenViewModel welcomeScreenViewModel)
    {
        Id = "CentralDocumentDock";
        IsCollapsable = false;
        CanClose = false;

        VisibleDockables = new ObservableCollection<IDockable>();
    }
}