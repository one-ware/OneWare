using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.Logging;
using OneWare.ChatBot.Services;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ChatBot.ViewModels;

public class AiEditViewModel : Document, INoSerializeLayout
{
    public string Original { get; set; }

    public AiEditViewModel(string filePath, string originalText)
    {
        FullPath = filePath;
        Original = originalText;
        Title = $"Edit {Path.GetFileName(filePath)}";
        LanguageExtension = Path.GetExtension(filePath);
    }

    public string FullPath { get; }

    public ICollection<ComparisonControlSection>? Chunks
    {
        get => field;
        set => SetProperty(ref field, value);
    }
    
    public string FileName => Path.GetFileName(FullPath);
    
    public string AddedLines => $"+{Chunks?.Sum(x => x.RightDiff.Where(x => x.Style == DiffContext.Added).Count()) ?? 0}";
    
    public string RemovedLines => $"-{Chunks?.Sum(x => x.RightDiff.Where(x => x.Style == DiffContext.Deleted).Count()) ?? 0}";

    public string LanguageExtension { get; }

    public async Task RefreshChanges(string modifiedText)
    {
        try
        {
            if (Original == null) throw new NullReferenceException(nameof(Original));

            Chunks = await Task.Run(() => DiffHelper.BuildDiff(Original, modifiedText));
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    public async Task UndoAsync()
    {
        await ContainerLocator.Container.Resolve<AiFileEditService>().UndoAsync(this);
    }
    
    public async Task AcceptAsync()
    {
        await ContainerLocator.Container.Resolve<AiFileEditService>().AcceptAsync(this);
    }
}