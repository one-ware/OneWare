using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Logging;
using OneWare.Chat.ViewModels;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Chat.Services;

public class AiFileEditService(IMainDockService mainDockService)
{
    public ObservableCollection<AiEditViewModel> ActiveEdits { get; } = new();
    private readonly Dictionary<string, string> _currentEdits = new();
    
    public async Task<string?> ReadFileAsync(string filePath, int? startLine = null, int? lineCount = null)
    {
        var openEditTab = mainDockService.OpenFiles.FirstOrDefault(x => x.Value.FullPath == filePath).Value as IEditor;

        if (startLine is null && lineCount is null)
            return openEditTab?.CurrentDocument.Text ?? (File.Exists(filePath) ? await File.ReadAllTextAsync(filePath) : null);

        if (startLine is null || startLine < 1)
            return null;

        if (lineCount is not null && lineCount < 0)
            return null;

        if (openEditTab?.CurrentDocument is { } document)
            return ReadFromDocument(document, startLine.Value, lineCount);

        if (!File.Exists(filePath))
            return null;

        return await ReadFromFileAsync(filePath, startLine.Value, lineCount);
    }
    
    public async Task<bool> EditFileAsync(string filePath, string newContent, int? startLine = null, int? lineCount = null)
    {
        var openTab = ActiveEdits.FirstOrDefault(x => x.FullPath == filePath);
        
        if (openTab == null)
        {
            var openEditTab = mainDockService.OpenFiles.FirstOrDefault(x => x.Value.FullPath == filePath).Value as IEditor;
            
            var original = openEditTab?.CurrentDocument.Text ?? (File.Exists(filePath) ? await File.ReadAllTextAsync(filePath) : string.Empty);
            openTab = new AiEditViewModel(filePath, original);
            
            ActiveEdits.Add(openTab);
            _currentEdits[filePath] = original;
        }
        else if (!_currentEdits.ContainsKey(filePath))
        {
            _currentEdits[filePath] = openTab.Original;
        }

        mainDockService.Show(openTab, DockShowLocation.Document);

        try
        {
            EnsureParentDirectoryExists(filePath);

            if (startLine is null && lineCount is null)
            {
                await File.WriteAllTextAsync(filePath, newContent);
                _currentEdits[filePath] = newContent;
                await openTab.RefreshChanges(newContent);
            }
            else
            {
                if (startLine is null || startLine < 1)
                    return false;

                if (lineCount is null || lineCount < 0)
                    return false;

                var currentContent = _currentEdits.TryGetValue(filePath, out var value) ? value : openTab.Original;
                var updatedContent = ApplyLineEdit(currentContent, startLine.Value, lineCount.Value, newContent);
                await File.WriteAllTextAsync(filePath, updatedContent);
                _currentEdits[filePath] = updatedContent;
                await openTab.RefreshChanges(updatedContent);
            }
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
            _currentEdits.Remove(edit.FullPath);
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
            _currentEdits.Remove(edit.FullPath);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
        
        return Task.CompletedTask;
    }

    public async Task UndoAllAsync()
    {
        foreach (var edit in ActiveEdits)
        {
            await UndoAsync(edit);
        }
    }

    private static string ReadFromDocument(AvaloniaEdit.Document.TextDocument document, int startLine, int? lineCount)
    {
        if (startLine > document.LineCount)
            return string.Empty;

        var start = document.GetLineByNumber(startLine);
        var lastLineNumber = lineCount is null
            ? document.LineCount
            : Math.Min(document.LineCount, startLine + lineCount.Value - 1);
        var end = document.GetLineByNumber(lastLineNumber);
        var length = end.Offset + end.TotalLength - start.Offset;
        return document.GetText(start.Offset, length);
    }

    private static async Task<string> ReadFromFileAsync(string filePath, int startLine, int? lineCount)
    {
        var builder = new StringBuilder();
        using var reader = new StreamReader(filePath);

        var currentLine = 1;
        var endLineExclusive = lineCount is null ? int.MaxValue : startLine + lineCount.Value;
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;

            if (currentLine >= startLine && currentLine < endLineExclusive)
                builder.AppendLine(line);

            if (currentLine >= endLineExclusive)
                break;

            currentLine++;
        }

        return builder.ToString();
    }

    private static string ApplyLineEdit(string original, int startLine, int lineCount, string newContent)
    {
        var newline = DetectNewLineFromText(original) ?? Environment.NewLine;
        var lines = SplitLines(original).ToList();
        var startIndex = Math.Clamp(startLine - 1, 0, lines.Count);
        var removeCount = Math.Clamp(lineCount, 0, Math.Max(0, lines.Count - startIndex));

        if (removeCount > 0)
            lines.RemoveRange(startIndex, removeCount);

        var insertLines = SplitLines(newContent);
        if (insertLines.Length > 0)
            lines.InsertRange(startIndex, insertLines);

        return string.Join(newline, lines);
    }

    private static string[] SplitLines(string content)
    {
        if (content.Length == 0)
            return Array.Empty<string>();
        return content.Replace("\r\n", "\n").Split('\n');
    }

    private static string? DetectNewLineFromText(string content)
    {
        var index = content.IndexOf('\n');
        if (index < 0)
            return null;
        return index > 0 && content[index - 1] == '\r' ? "\r\n" : "\n";
    }

    private static void EnsureParentDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

}
