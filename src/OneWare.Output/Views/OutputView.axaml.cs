using System.Text.RegularExpressions;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Output.ViewModels;
using Prism.Ioc;

namespace OneWare.Output.Views;

public partial class OutputView : OutputBaseView
{
    private readonly TextModificationService _modificationService;

    private PointerEventArgs? _lastMovedArgs;
    
    private SearchResult? _searchResult;
    
    public OutputView()
    {
        InitializeComponent();
        
        _modificationService = new TextModificationService(Output.TextArea.TextView);
        Output.TextArea.TextView.LineTransformers.Add(_modificationService);
        Output.Options.AllowScrollBelowDocument = false;
        
        Output.AddHandler(PointerPressedEvent, PointerPressedAfterCaretUpdate, RoutingStrategies.Bubble, true);

    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        _lastMovedArgs = e;

        if (e.KeyModifiers == PlatformHelper.ControlKey)
        {
            SearchPath();
        }
        else
        {
           ResetControlModification();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(e.KeyModifiers == PlatformHelper.ControlKey)
        {
            SearchPath();
        }
        base.OnKeyDown(e);
    }
    
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if(e.KeyModifiers == PlatformHelper.ControlKey)
        {
            ResetControlModification();
        }
        base.OnKeyUp(e);
    }

    private void PointerPressedAfterCaretUpdate(object? sender, PointerPressedEventArgs e)
    {
        if(!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;

        _ = OpenFileAsync();
    }
    
    private async Task OpenFileAsync()
    {
        if(_searchResult == null) return;
        
        var result = ContainerLocator.Container.Resolve<IProjectExplorerService>()
            .ActiveProject?.SearchRelativePath(_searchResult.Path);

        if (result is IFile file)
        {
            var doc = await ContainerLocator.Container.Resolve<IDockService>().OpenFileAsync(file);
            if (doc is not IEditor evb) return;

            var offset = evb.CurrentDocument.GetOffset(_searchResult.Line, _searchResult.Column);
            
            evb.Select(offset, 0);
        }
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        ResetControlModification();
        base.OnLostFocus(e);
    }
    
    private void SearchPath()
    {
        if(_lastMovedArgs == null) return;
        
        var pointerPosition = Output.GetOffsetFromPointerPosition(_lastMovedArgs);
            
        if(pointerPosition < 0) return;
            
        var line = Output.Document.GetLineByOffset(pointerPosition);
        var lineText = Output.Document.GetText(line);
            
        var regex = ExtractFilePathRegex();

        var matches = regex.Matches(lineText);

        var match = matches.FirstOrDefault(x => x.Index + line.Offset <= pointerPosition && x.Index + line.Offset + x.Length >= pointerPosition);

        if (match is not { Success: true }) return;
            
        var path = match.Groups[1].Value;
        var fileLine = int.Parse(match.Groups[2].Value);
        var fileColumn = int.Parse(match.Groups[3].Value);
        
        _searchResult = new SearchResult(path, fileLine, fileColumn);

        var lineContext = (DataContext as OutputBaseViewModel)?.LineContexts[line.LineNumber-1];

        var result = lineContext?.Owner?.SearchRelativePath(_searchResult.Path);
        
        if(result == null) return;
        
        Output.TextArea.TextView.Cursor = Cursor.Parse("Hand");
            
        _modificationService.SetModification("Control_Underline", new TextModificationSegment(
                line.Offset + match.Index,
                line.Offset + match.Index + match.Length)
            { Decorations = TextDecorationCollection.Parse("Underline") });
    }

    private void ResetControlModification()
    {
        Output.TextArea.TextView.Cursor = Cursor.Parse("IBeam");
        _modificationService.ClearModification("Control_Underline");
        _searchResult = null;
    }

    [GeneratedRegex(@"^(.*?):(\d+):(\d+)")]
    private static partial Regex ExtractFilePathRegex();
    
    private class SearchResult
    {
        public string Path { get; }
        public int Line { get; }
        public int Column { get; }
        
        public SearchResult(string path, int line, int column)
        {
            Path = path;
            Line = line;
            Column = column;
        }
    }
}