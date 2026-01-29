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
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(AddedLines));
            OnPropertyChanged(nameof(RemovedLines));
        }
    }

    public string FileName => Path.GetFileName(FullPath);
    
    public string AddedLines => $"+{Chunks?.Sum(x => x.RightDiff.Count(b => b.Style is DiffContext.Added)) ?? 0}";
    
    public string RemovedLines => $"-{Chunks?.Sum(x => x.LeftDiff.Count(b => b.Style is DiffContext.Deleted)) ?? 0}";

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