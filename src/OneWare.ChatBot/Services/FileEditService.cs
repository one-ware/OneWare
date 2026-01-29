using OneWare.Essentials.Services;

namespace OneWare.ChatBot.Services;

public class FileEditService(IProjectExplorerService projectExplorerService, IMainDockService mainDockService)
{
    public async Task ProposeEditAsync(string filePath, string newContent)
    {
       
    }
}