using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using OneWare.Essentials.EditorExtensions;
using ReactiveUI;
using Pair = System.Collections.Generic.KeyValuePair<int, Avalonia.Controls.Control>;

namespace OneWare.SourceControl
{
    public enum MergeMode
    {
        KeepCurrent,
        KeepIncoming,
        KeepBoth
    }

    public class MergeService : IBackgroundRenderer
    {
        private readonly TextEditor _textEditor;

        public IBrush BorderBrush = new SolidColorBrush(Color.FromArgb(255, 57, 57, 57));
        public IBrush CurrentBackgroundBrush = new SolidColorBrush(Color.FromArgb(50, 50, 100, 140));

        public IBrush HeadBackgroundBrush = new SolidColorBrush(Color.FromArgb(50, 50, 115, 100));

        public MergeService(TextEditor textEditor, ElementGenerator elementGenerator)
        {
            _textEditor = textEditor;

            BorderPen = new Pen(Brushes.Transparent);

            textEditor.TextChanged += (_, _) =>
            {
                Merges = GetMerges(textEditor.Document);

                elementGenerator.Controls.Clear();

                foreach (var merge in Merges)
                {
                    var stack = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(30, _textEditor.FontSize / 7d, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    stack.Children.Add(
                        new Button
                        {
                            Content = "Keep HEAD",
                            Command = ReactiveCommand.Create<MergeEntry>(MergeKeepCurrent),
                            CommandParameter = merge,
                            Classes = { "MergeButton" }
                        });

                    stack.Children.Add(new TextBlock { Text = "|", Classes = {"MergeButtonSeparator"} });

                    stack.Children.Add(
                        new Button
                        {
                            Content = "Keep Incoming",
                            Classes = { "MergeButton" },
                            Command = ReactiveCommand.Create<MergeEntry>(MergeKeepIncoming),
                            CommandParameter = merge
                        });

                    stack.Children.Add(new TextBlock { Text = "|", Classes = {"MergeButtonSeparator"} });

                    stack.Children.Add(
                        new Button
                        {
                            Content = "Keep Both",
                            Classes = { "MergeButton" },
                            Command = ReactiveCommand.Create<MergeEntry>(MergeKeepBoth),
                            CommandParameter = merge
                        });

                    _textEditor.TextArea.TextView.Redraw();

                    elementGenerator.Controls.Add(new Pair(merge.StartIndex + "<<<<<<< HEAD".Length, stack)!);
                }
            };
        }

        public Pen BorderPen { get; set; }

        public List<MergeEntry> Merges { get; private set; } = new();

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_textEditor.Document != null)
                foreach (var merge in Merges)
                {
                    DrawLine(merge.StartLine, HeadBackgroundBrush, textView, drawingContext);
                    for (var i = merge.StartLine; i < merge.MidLine; i++)
                        DrawLine(i, HeadBackgroundBrush, textView, drawingContext);

                    DrawLine(merge.EndLine, HeadBackgroundBrush, textView, drawingContext);
                    for (var i = merge.MidLine + 1; i <= merge.EndLine; i++)
                        DrawLine(i, CurrentBackgroundBrush, textView, drawingContext);
                }
        }

        public void DrawLine(int line, IBrush brush, TextView textView, DrawingContext drawingContext)
        {
            var visualLine = textView.GetVisualLine(line);
            if (visualLine == null) return;

            var builder = new BackgroundGeometryBuilder();

            var linePosY = visualLine.VisualTop - textView.ScrollOffset.Y;
            var lineBottom = linePosY + visualLine.Height;

            var pixelSize = PixelSnapHelpers.GetPixelSize(textView);


            var x = PixelSnapHelpers.PixelAlign(0, pixelSize.Width);
            var y = PixelSnapHelpers.PixelAlign(linePosY, pixelSize.Height);
            var x2 = PixelSnapHelpers.PixelAlign(textView.Bounds.Width - pixelSize.Width, pixelSize.Width);
            var y2 = PixelSnapHelpers.PixelAlign(lineBottom, pixelSize.Height);

            builder.AddRectangle(textView, new Rect(new Point(x, y), new Point(x2, y2)));

            var geometry = builder.CreateGeometry();
            if (geometry != null) drawingContext.DrawGeometry(brush, BorderPen, geometry);
        }

        public void MergeKeepIncoming(MergeEntry entry)
        {
            Merge(entry, MergeMode.KeepIncoming);
        }

        public void MergeKeepCurrent(MergeEntry entry)
        {
            Merge(entry, MergeMode.KeepCurrent);
        }

        public void MergeKeepBoth(MergeEntry entry)
        {
            Merge(entry, MergeMode.KeepBoth);
        }

        public void Merge(MergeEntry entry, MergeMode mode)
        {
            Merge(_textEditor.Document, entry, mode);
        }

        public static void Merge(TextDocument document, MergeEntry entry, MergeMode mode)
        {
            document.BeginUpdate();

            if (mode == MergeMode.KeepBoth)
            {
                document.Remove(entry.EndIndex - 2, document.GetLineByNumber(entry.EndLine).Length + 2);
                document.Remove(entry.MidIndex - 2, document.GetLineByNumber(entry.MidLine).Length + 2);
                document.Remove(entry.StartIndex - 2, document.GetLineByNumber(entry.StartLine).Length + 2);
            }
            else if (mode == MergeMode.KeepIncoming)
            {
                document.Remove(entry.EndIndex - 2, document.GetLineByNumber(entry.EndLine).Length + 2);
                document.Remove(entry.StartIndex - 2,
                    entry.MidIndex - entry.StartIndex + document.GetLineByNumber(entry.MidLine).Length + 2);
            }
            else if (mode == MergeMode.KeepCurrent)
            {
                document.Remove(entry.MidIndex - 2,
                    entry.EndIndex - entry.MidIndex + document.GetLineByNumber(entry.EndLine).Length + 2);
                document.Remove(entry.StartIndex - 2, document.GetLineByNumber(entry.StartLine).Length + 2);
            }

            document.EndUpdate();
        }

        public static List<MergeEntry> GetMerges(TextDocument document)
        {
            var merges = new List<MergeEntry>();

            //Search for merging
            var lastIndex = 0;
            while (document.Text.IndexOf("<<<<<<< HEAD", lastIndex, StringComparison.Ordinal) is var sIndex &&
                   sIndex > 0 &&
                   document.Text.IndexOf("=======", sIndex, StringComparison.Ordinal) is var mIndex && mIndex > 0 &&
                   document.Text.IndexOf(">>>>>>>", mIndex, StringComparison.Ordinal) is var eIndex && eIndex > 0)
            {
                var sLine = document.GetLineByOffset(sIndex).LineNumber;
                var mLine = document.GetLineByOffset(mIndex).LineNumber;
                var eLine = document.GetLineByOffset(eIndex).LineNumber;

                var merge = new MergeEntry(sIndex, sLine, mIndex, mLine, eIndex, eLine);
                merges.Add(merge);

                lastIndex = eIndex + 7;
            }

            return merges;
        }
    }

    public class MergeEntry
    {
        public MergeEntry(int startIndex, int startLine, int midIndex, int midLine, int endIndex, int endLine)
        {
            StartIndex = startIndex;
            StartLine = startLine;
            MidIndex = midIndex;
            MidLine = midLine;
            EndIndex = endIndex;
            EndLine = endLine;
        }

        public int StartIndex { get; }
        public int StartLine { get; }
        public int MidIndex { get; }
        public int MidLine { get; }
        public int EndIndex { get; }
        public int EndLine { get; }
    }
}