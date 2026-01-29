using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ChatBot.ViewModels;

public class AiEditViewModel : ExtendedDocument
{
    public AiEditViewModel(string fullPath, IProjectExplorerService projectExplorerService, IMainDockService mainDockService, IWindowService windowService) : base(fullPath, projectExplorerService, mainDockService, windowService)
    {
    }

    protected override void UpdateCurrentFile(IFile? oldFile)
    {
        
    }
}