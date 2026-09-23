using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.Logging;
using OneWare.Chat.Services;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Chat.ViewModels;

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

    /// <summary>
    /// One-based line of the rendered diff that should be brought into view, or zero for none.
    /// </summary>
    public int ScrollToLine
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public async Task RefreshChanges(string modifiedText, int? focusLine = null)
    {
        try
        {
            if (Original == null) throw new NullReferenceException(nameof(Original));

            var chunks = await Task.Run(() => DiffHelper.BuildDiff(Original, modifiedText));
            Chunks = chunks;
            ScrollToLine = GetScrollTargetLine(chunks, focusLine);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    /// <summary>
    /// Translates a line of the modified file into a line of the rendered diff. The diff inserts blank
    /// filler lines to keep both sides aligned, so the rendered line number does not match
    /// <see cref="DiffLineModel.LineNumber" /> and has to be resolved through the list index.
    /// </summary>
    private static int GetScrollTargetLine(IReadOnlyList<ComparisonControlSection> chunks, int? focusLine)
    {
        if (chunks.Count != 1) return 0;

        var rightDiff = chunks[0].RightDiff;

        if (focusLine is > 0)
        {
            for (var i = 0; i < rightDiff.Count; i++)
            {
                if (rightDiff[i].Style is not DiffContext.Blank && rightDiff[i].LineNumber >= focusLine)
                    return i + 1;
            }
        }

        for (var i = 0; i < rightDiff.Count; i++)
        {
            if (rightDiff[i].Style is DiffContext.Added or DiffContext.Deleted)
                return i + 1;
        }

        return 0;
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