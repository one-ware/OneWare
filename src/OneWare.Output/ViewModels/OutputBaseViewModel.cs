using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Output.ViewModels;

public abstract class OutputBaseViewModel : ExtendedTool
{
    private const int Maxoutputlength = 50000;

    private const int Maxlinelength = 1000;

    private bool _autoScroll = true;

    private int _caretIndex;

    private int _currentLineLength;

    private int _currentLineNumber = 1;

    private bool _isLoading;

    private TextDocument _outputDocument = new();

    public OutputBaseViewModel(string iconKey) : base(iconKey)
    {
    }

    public TextDocument OutputDocument
    {
        get => _outputDocument;
        private set => SetProperty(ref _outputDocument, value);
    }

    public int CaretIndex
    {
        get => _caretIndex;
        set => SetProperty(ref _caretIndex, value);
    }

    public ObservableCollection<LineContext> LineContexts { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    public void WriteLine(string text, IBrush? textColor = null)
    {
        Write(text + "\n", textColor);
    }

    public void Write(string text, IBrush? textColor = null, IProjectRoot? owner = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        OutputDocument.BeginUpdate();

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _currentLineNumber++;
                LineContexts.Add(new LineContext()
                {
                    LineColor = textColor,
                    Owner = owner
                });
                _currentLineLength = 0;
            }
            else
            {
                _currentLineLength++;
            }

            if (_currentLineLength > Maxlinelength && i != text.Length - 1) text = text.Insert(i + 1, "\n");
        }

        OutputDocument.Insert(OutputDocument.TextLength, text);

        if (OutputDocument.TextLength > Maxoutputlength)
        {
            var removeLines = OutputDocument.Text[..(OutputDocument.TextLength - Maxoutputlength)]
                .Split('\n').Length - 1;
            for (var i = 0; i < removeLines; i++) LineContexts.RemoveAt(0);
            OutputDocument.Remove(0, OutputDocument.TextLength - Maxoutputlength);
        }

        OutputDocument.EndUpdate();
    }

    public void Clear()
    {
        OutputDocument.Text = "";
        OutputDocument.UndoStack.ClearAll();
        LineContexts.Clear();
        _currentLineNumber = 1;
    }
}