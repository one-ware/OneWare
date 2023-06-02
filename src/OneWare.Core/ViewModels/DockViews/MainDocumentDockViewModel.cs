namespace OneWare.Core.ViewModels.DockViews;

public class MainDocumentDockViewModel : CustomDocumentDock
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