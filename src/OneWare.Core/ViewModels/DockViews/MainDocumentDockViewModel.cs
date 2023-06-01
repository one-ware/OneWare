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
        
        //ActiveDockable = welcomeScreenViewModel;
        // _visibleDockables.CollectionChanged += (sender, args) =>
        // {
        //     if (_visibleDockables.Count == 0)
        //     {
        //         _visibleDockables.Add();
        //     }
        // };
    }
    
    
}