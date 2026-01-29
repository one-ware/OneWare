using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using OneWare.ChatBot.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ChatBot.Services;

public class FileEditService(IMainDockService mainDockService)
{
    public ObservableCollection<AiEditViewModel> ActiveEdits { get; } = new();

    public async Task<string?> ReadFileAsync(string filePath)
    {
        var openEditTab = mainDockService.OpenFiles.FirstOrDefault(x => x.Value.FullPath == filePath).Value as IEditor;

        return openEditTab?.CurrentDocument.Text ?? await File.ReadAllTextAsync(filePath);
    }
    
    public async Task<bool> EditFileAsync(string filePath, string newContent)
    {
        var openTab = ActiveEdits.FirstOrDefault(x => x.FullPath == filePath);
        
        if (openTab == null)
        {
            var openEditTab = mainDockService.OpenFiles.FirstOrDefault(x => x.Value.FullPath == filePath).Value as IEditor;
            
            var original = openEditTab?.CurrentDocument.Text ?? await File.ReadAllTextAsync(filePath);
            openTab = new AiEditViewModel(filePath, original);
            
            ActiveEdits.Add(openTab);
        }

        mainDockService.Show(openTab, DockShowLocation.Document);

        try
        {
            await File.WriteAllTextAsync(filePath, newContent);
            await openTab.RefreshChanges(newContent);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
        
        return true;
    }

    public async Task UndoAsync(AiEditViewModel edit)
    {

        try
        {
            await File.WriteAllTextAsync(edit.FullPath, edit.Original);
            mainDockService.CloseDockable(edit);
            ActiveEdits.Remove(edit);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }
    
    public Task AcceptAsync(AiEditViewModel edit)
    {
        try
        {
            mainDockService.CloseDockable(edit);
            ActiveEdits.Remove(edit);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
        
        return Task.CompletedTask;
    }
}