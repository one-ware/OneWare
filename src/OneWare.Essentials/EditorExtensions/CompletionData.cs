using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Snippets;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OneWare.Essentials.EditorExtensions;

public partial class CompletionData : ICompletionData
{
    public readonly CompletionItem? CompletionItemLsp;

    public CompletionData(string insertText, string label, string? detail, string? description, IImage? icon,
        double priority,
        CompletionItem completionItem, int offset, string? fullPath, Action? afterCompletion = null)
    {
        InsertText = insertText;
        Label = label;
        Detail = detail;
        Description = description;
        Image = icon;
        Priority = priority;
        CompletionItemLsp = completionItem;
        AfterCompletion = afterCompletion;
        FullPath = fullPath;
        CompletionOffset = offset;
    }

    public CompletionData(string insertText, string label, string? detail, string? description, IImage? icon,
        double priority, int offset, string fullPath, Action? afterCompletion = null)
    {
        InsertText = insertText;
        Label = label;
        Detail = detail;
        Description = description;
        Image = icon;
        Priority = priority;
        FullPath = fullPath;
        AfterCompletion = afterCompletion;
        CompletionOffset = offset;
    }

    private Action? AfterCompletion { get; }

    public int CompletionOffset { get; }

    public string? Detail { get; }

    public string? FullPath { get; set; }

    public IImage? Image { get; }

    public string InsertText { get; }

    public string Label { get; }

    public object? Description { get; }

    public double Priority { get; }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var segmentLine = textArea.Document.GetLineByOffset(completionSegment.Offset);

        var placeHolder = PlaceHolderRegex();

        var newLine = TextUtilities.GetNewLineFromDocument(textArea.Document, segmentLine.LineNumber);

        var formattedText = InsertText.Replace("\r", "").Replace("\n", newLine)
            .Replace("\t", textArea.Options.IndentationString);

        var filteredText = formattedText!;

        var snippet = new Snippet();

        var placeHolders = new Dictionary<string, SnippetReplaceableTextElement>();

        while (placeHolder.Match(filteredText) is { Success: true } match)
        {
            var before = filteredText[..match.Index];
            filteredText = filteredText.Remove(0, match.Index + match.Length);

            if (before.Length > 0)
                snippet.Elements.Add(new SnippetTextElement
                {
                    Text = before
                });

            if (match.Groups[2].Success)
            {
                if (match.Groups[3].Success)
                {
                    var element = new SnippetReplaceableTextElement
                    {
                        Text = ReplaceVariables(match.Groups[3].Value)
                    };

                    snippet.Elements.Add(element);
                    placeHolders.Add(match.Groups[2].Value, element);
                }
                else if (match.Groups[4].Success)
                {
                    var element = new SnippetReplaceableTextElement
                    {
                        Text = match.Groups[4].Value
                    };

                    snippet.Elements.Add(element);
                    placeHolders.Add(match.Groups[2].Value, element);
                }
            }
            else if (match.Groups[1].Success)
            {
                if (placeHolders.TryGetValue(match.Groups[1].Value, out var boundElement))
                    snippet.Elements.Add(new SnippetBoundElement
                    {
                        TargetElement = boundElement
                    });
                else
                    snippet.Elements.Add(new SnippetCaretElement());
            }
        }

        if (filteredText.Length > 0)
            snippet.Elements.Add(new SnippetTextElement
            {
                Text = filteredText
            });

        textArea.Document.BeginUpdate();

        textArea.Document.Replace(completionSegment, "");

        snippet.Insert(textArea);

        textArea.Document.EndUpdate();

        AfterCompletion?.Invoke();
    }

    [GeneratedRegex(@"\$(\d+)|\$\{(\d+)(?::([^}|]+))?(?:\|([^}]+)\|)?\}")]
    private static partial Regex PlaceHolderRegex();

    private string ReplaceVariables(string input)
    {
        return input.Replace("$TM_FILENAME_BASE", Path.GetFileNameWithoutExtension(FullPath));
    }
}
