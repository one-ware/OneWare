using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Snippets;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OneWare.Essentials.EditorExtensions
{
    public class CompletionData : ICompletionData
    {
        public readonly CompletionItem? CompletionItemLsp;

        private Action? AfterCompletion { get; }

        public int CompletionOffset { get; }

        public IImage? Image { get; }

        public string Text { get; private set; }

        public object Content { get; }

        public string? Detail { get; }

        public object? Description { get; }

        public double Priority { get; }

        public CompletionData(string insertText, string label, string? detail, string? description, IImage? icon,
            double priority,
            CompletionItem completionItem, int offset, Action? afterCompletion = null)
        {
            Text = insertText;
            Content = label;
            Detail = detail;
            Description = description;
            Image = icon;
            Priority = priority;
            CompletionItemLsp = completionItem;
            AfterCompletion = afterCompletion;
            CompletionOffset = offset;
        }

        public CompletionData(string insertText, string label, string? detail, string? description, IImage? icon,
            double priority, int offset, Action? afterCompletion = null)
        {
            Text = insertText;
            Content = label;
            Detail = detail;
            Description = description;
            Image = icon;
            Priority = priority;
            AfterCompletion = afterCompletion;
            CompletionOffset = offset;
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var segmentLine = textArea.Document.GetLineByOffset(completionSegment.Offset);

            var placeHolder = new Regex(@"\$(\d+)|\$\{(\d+)(?::(<[^}|]+>))?(?:\|([^}]+)\|)?\}");

            var newLine = TextUtilities.GetNewLineFromDocument(textArea.Document, segmentLine.LineNumber);

            var formattedText = Text.Replace("\r", "").Replace("\n", newLine)
                .Replace("\t", textArea.Options.IndentationString);

            var filteredText = formattedText!;

            var snippet = new Snippet();

            var placeHolders = new Dictionary<string, SnippetReplaceableTextElement>();
            
            while (placeHolder.Match(filteredText) is { Success: true } match)
            {
                var before = filteredText[..match.Index];
                filteredText = filteredText.Remove(0, match.Index + match.Length);
                
                if(before.Length > 0)
                    snippet.Elements.Add(new SnippetTextElement()
                    {
                        Text = before
                    });

                if (match.Groups[2].Success)
                {
                    if (match.Groups[3].Success)
                    {
                        var element = new SnippetReplaceableTextElement()
                        {
                            Text = match.Groups[3].Value
                        };
                        
                        snippet.Elements.Add(element);
                        placeHolders.Add(match.Groups[2].Value, element);
                    }
                    else if (match.Groups[4].Success)
                    {
                        var element = new SnippetReplaceableTextElement()
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
                    {
                        snippet.Elements.Add(new SnippetBoundElement()
                        {
                            TargetElement = boundElement
                        });
                    }
                    else
                    {
                        snippet.Elements.Add(new SnippetCaretElement());
                    }
                }
            }

            if(filteredText.Length > 0)
                snippet.Elements.Add(new SnippetTextElement()
                {
                    Text = filteredText
                });
            
            textArea.Document.BeginUpdate();
            
            textArea.Document.Replace(completionSegment, "");
            
            // //Remove chars made within async delay
            if (textArea.Caret.Offset > CompletionOffset - 1)
            {
                //textArea.Document.Replace(CompletionOffset - 1, textArea.Caret.Offset - (CompletionOffset - 1), ""); 
            }
            
            snippet.Insert(textArea);
            
            textArea.Document.EndUpdate();
            
            // if (!placeHolder.Match(formattedText).Success && !placeHolder.Match(formattedText).Success) formattedText += "${1}";
            //
            // var startLine = segmentLine.LineNumber;
            // var endLine = segmentLine.LineNumber + formattedText.Split('\n').Length - 1;
            //
            // textArea.Document.BeginUpdate();
            //
          
            //
            // textArea.Document.Replace(completionSegment, formattedText);
            //
            // var start = textArea.Document.GetLineByNumber(startLine);
            // var end = textArea.Document.GetLineByNumber(endLine);
            //
            // textArea.IndentationStrategy?.IndentLines(textArea.Document, segmentLine.LineNumber, endLine);
            //
            // var indentedText = textArea.Document.Text.Substring(start.Offset, end.EndOffset - start.Offset);
            //
            // var snippetContext = new SnippetProcessingContext();
            //
            // var filteredText = indentedText;
            //
            // while(placeHolder.Match(filteredText) is {Success: true} match)
            // {
            //     filteredText = filteredText.Remove(match.Index, match.Length);
            //     
            //     var startOffset = start.Offset + match.Index;
            //     var endOffset = start.Offset + match.Index + match.Length;
            //     var options = new List<string>();
            //
            //     filteredText = filteredText.Remove(match.Index, match.Length);
            //     
            //     if (match.Groups[1].Success)
            //     {
            //         options.Add(match.Groups[1].Value);
            //         filteredText = filteredText.Insert(match.Index, match.Groups[1].Value);
            //     }
            //     else if (match.Groups[2].Success)
            //     {
            //         options.AddRange(match.Groups[2].Value.Split(','));
            //         filteredText = filteredText.Insert(match.Index, options.FirstOrDefault() ?? "");
            //     }
            //
            //     snippetContext.PlaceHolders.Add(new SnippetPlaceHolder(startOffset, endOffset, options));
            // }
            //
            // textArea.Document.Replace(start.Offset, end.EndOffset - start.Offset, filteredText);
            //
            // textArea.Document.EndUpdate();
            //
            
            AfterCompletion?.Invoke();
        }
    }
}