using OneWare.Essentials.Models;
using OneWare.ProjectExplorer.ViewModels;

namespace OneWare.LibraryExplorer.ViewModels;

public class LibraryExplorerViewModel : ProjectViewModelBase
{
    public const string IconKey = "BoxIcons.RegularLibrary";

    public LibraryExplorerViewModel() : base(IconKey)
    {
        Id = "LibraryExplorer";
        Title = "Library Explorer";
    }
    
    public void DoubleTab(IProjectEntry entry)
    {
        
    }
}