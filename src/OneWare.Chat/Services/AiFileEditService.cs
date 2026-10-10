using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Logging;
using OneWare.Chat.ViewModels;
using Avalonia.Controls.Notifications;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Chat.Services;

public class AiFileEditService(IMainDockService mainDockService, ISettingsService settingsService)
{
    public const string AutoOpenFileEditsKey = "Chat_AutoOpenFileEdits";

    /// <summary>
    /// Unreviewed AI edits of all chats. A file edited by several chats has one entry per chat; see
    /// <see cref="AiEditViewModel.OwnerId" />.
    /// </summary>
    public ObservableCollection<AiEditViewModel> ActiveEdits { get; } = new();

    // Latest content the AI wrote per file (shared by all chats, the file exists once on disk).
    private readonly Dictionary<string, string> _currentEdits = new();

    /// <summary>The unreviewed edits of one chat; null lists edits made without a chat.</summary>
    public IEnumerable<AiEditViewModel> GetEdits(string? ownerId) =>
        ActiveEdits.Where(x => string.Equals(x.OwnerId, ownerId, StringComparison.Ordinal));
    
    public async Task<string?> ReadFileAsync(string filePath, int? startLine = null, int? lineCount = null)
    {
        // While an AI edit is in progress, read from the same working buffer that EditFileAsync mutates.
        // Otherwise reads come from the (stale) live editor document or disk, so line numbers no longer
        // match the edit buffer and subsequent incremental edits get misaligned.
        if (_currentEdits.TryGetValue(filePath, out var pending))
        {
            if (startLine is null && lineCount is null)
                return pending;

            if (startLine is null || startLine < 1)
                return null;

            if (lineCount is not null && lineCount < 0)
                return null;

            return ReadFromText(pending, startLine.Value, lineCount);
        }

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
    
    public Task<bool> EditFileAsync(string filePath, string newContent, int? startLine = null, int? lineCount = null)
        => EditFileAsync(filePath, newContent, startLine, lineCount, null);

    /// <param name="ownerId">The chat (session id) making the edit; it lists and rolls back its own edits.</param>
    public async Task<bool> EditFileAsync(string filePath, string newContent, int? startLine, int? lineCount,
        string? ownerId)
    {
        var openTab = ActiveEdits.FirstOrDefault(x =>
            x.FullPath == filePath && string.Equals(x.OwnerId, ownerId, StringComparison.Ordinal));
        
        if (openTab == null)
        {
            // Another chat may have edited the file already; this chat's change starts from that state.
            if (!_currentEdits.TryGetValue(filePath, out var original))
            {
                var openEditTab = mainDockService.OpenFiles.FirstOrDefault(x => x.Value.FullPath == filePath).Value as IEditor;
                original = openEditTab?.CurrentDocument.Text ??
                           (File.Exists(filePath) ? await File.ReadAllTextAsync(filePath) : string.Empty);
            }

            openTab = new AiEditViewModel(filePath, original) { OwnerId = ownerId };
            
            ActiveEdits.Add(openTab);
            _currentEdits[filePath] = original;
        }
        else if (!_currentEdits.ContainsKey(filePath))
        {
            _currentEdits[filePath] = openTab.LastWritten ?? openTab.Original;
        }

        if (settingsService.GetSettingValue<bool>(AutoOpenFileEditsKey))
            mainDockService.Show(openTab, DockShowLocation.Document);

        try
        {
            EnsureParentDirectoryExists(filePath);

            if (startLine is null && lineCount is null)
            {
                await File.WriteAllTextAsync(filePath, newContent);
                _currentEdits[filePath] = newContent;
                openTab.LastWritten = newContent;
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
                openTab.LastWritten = updatedContent;
                await openTab.RefreshChanges(updatedContent, startLine.Value);
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
        // Rolling back restores the file as it was before this chat's first edit. If another chat edited the
        // file afterwards, that would silently drop the other chat's changes, so the rollback is refused.
        var changedSince = edit.LastWritten != null &&
                           _currentEdits.TryGetValue(edit.FullPath, out var current) &&
                           !string.Equals(current, edit.LastWritten, StringComparison.Ordinal);
        if (changedSince)
        {
            ContainerLocator.Container.Resolve<IWindowService>().ShowNotification("Rollback not possible",
                $"{edit.FileName} was changed by another chat afterwards. Roll back or accept that chat's changes first.",
                NotificationType.Warning);
            return;
        }

        try
        {
            await File.WriteAllTextAsync(edit.FullPath, edit.Original);
            mainDockService.CloseDockable(edit);
            ActiveEdits.Remove(edit);
            ForgetCurrentContent(edit.FullPath, edit.Original);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    /// <summary>Drops the working content of a file once no chat has unreviewed edits of it anymore.</summary>
    private void ForgetCurrentContent(string filePath, string? content = null)
    {
        if (ActiveEdits.Any(x => x.FullPath == filePath))
        {
            if (content != null) _currentEdits[filePath] = content;
            return;
        }

        _currentEdits.Remove(filePath);
    }
    
    public Task AcceptAsync(AiEditViewModel edit)
    {
        try
        {
            mainDockService.CloseDockable(edit);
            ActiveEdits.Remove(edit);
            ForgetCurrentContent(edit.FullPath);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
        
        return Task.CompletedTask;
    }

    public async Task UndoAllAsync()
    {
        // Newest first, so edits of one file by several chats unwind in order.
        foreach (var edit in ActiveEdits.Reverse().ToArray())
        {
            await UndoAsync(edit);
        }
    }

    public async Task AcceptAllAsync()
    {
        foreach (var edit in ActiveEdits.ToArray())
        {
            await AcceptAsync(edit);
        }
    }

    /// <summary>Rolls back the edits of one chat.</summary>
    public async Task UndoAllAsync(string? ownerId)
    {
        foreach (var edit in GetEdits(ownerId).Reverse().ToArray())
            await UndoAsync(edit);
    }

    /// <summary>Accepts the edits of one chat.</summary>
    public async Task AcceptAllAsync(string? ownerId)
    {
        foreach (var edit in GetEdits(ownerId).ToArray())
            await AcceptAsync(edit);
    }

    private static string ReadFromText(string content, int startLine, int? lineCount)
    {
        var lines = SplitLines(content);
        if (startLine > lines.Length)
            return string.Empty;

        var startIndex = startLine - 1;
        var available = lines.Length - startIndex;
        var take = lineCount is null ? available : Math.Min(lineCount.Value, available);
        if (take <= 0)
            return string.Empty;

        var newline = DetectNewLineFromText(content) ?? Environment.NewLine;
        return string.Join(newline, lines.Skip(startIndex).Take(take));
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

    internal static string ApplyLineEdit(string original, int startLine, int lineCount, string newContent)
    {
        var newline = DetectNewLineFromText(original) ?? Environment.NewLine;
        var lines = SplitLines(original).ToList();
        var startIndex = Math.Clamp(startLine - 1, 0, lines.Count);
        var removeCount = Math.Clamp(lineCount, 0, Math.Max(0, lines.Count - startIndex));

        if (removeCount > 0)
            lines.RemoveRange(startIndex, removeCount);

        var insertLines = SplitContentLines(newContent);
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

    /// <summary>
    /// Splits replacement content into lines, treating a single trailing newline as a line terminator
    /// instead of a separator, so that "X\n" yields one line and not an extra empty one.
    /// </summary>
    private static string[] SplitContentLines(string content)
    {
        if (content.Length == 0)
            return Array.Empty<string>();

        var normalized = content.Replace("\r\n", "\n");
        if (normalized.EndsWith('\n'))
            normalized = normalized[..^1];

        return normalized.Split('\n');
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
