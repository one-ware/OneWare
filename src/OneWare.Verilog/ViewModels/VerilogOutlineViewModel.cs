using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Verilog.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OneWare.Verilog.ViewModels;

public partial class VerilogOutlineViewModel : ExtendedTool
{
    public const string IconKey = "VsImageLib.Search16XMd";

    private readonly ILanguageManager _languageManager;
    private readonly IMainDockService _mainDockService;
    private IEditor? _currentEditor;
    private int _refreshVersion;

    [ObservableProperty] private VerilogSymbolNode? _selectedNode;

    public VerilogOutlineViewModel(IMainDockService mainDockService, ILanguageManager languageManager)
        : base(IconKey)
    {
        _mainDockService = mainDockService;
        _languageManager = languageManager;
        Id = "VerilogOutline";
        Title = "Outline";

        mainDockService.WhenValueChanged(x => x.CurrentDocument)
            .Subscribe(OnCurrentDocumentChanged);
    }

    public ObservableCollection<VerilogSymbolNode> Nodes { get; } = new();

    private void OnCurrentDocumentChanged(IExtendedDocument? currentDocument)
    {
        if (_currentEditor != null) _currentEditor.FileSaved -= OnCurrentFileSaved;
        _currentEditor = currentDocument as IEditor;
        if (_currentEditor != null) _currentEditor.FileSaved += OnCurrentFileSaved;
        _ = RefreshAsync();
    }

    private void OnCurrentFileSaved(object? sender, EventArgs e)
    {
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var version = ++_refreshVersion;
        if (_mainDockService.CurrentDocument is not IEditor editor ||
            !VerilogModule.SupportedExtensions.Contains(Path.GetExtension(editor.FullPath),
                StringComparer.OrdinalIgnoreCase))
        {
            Nodes.Clear();
            return;
        }

        var service = _languageManager.GetLanguageService(editor.FullPath);
        var symbols = service == null ? null : await service.RequestSymbolsAsync(editor.FullPath);
        if (version != _refreshVersion) return;

        Nodes.Clear();
        if (symbols is null) return;

        foreach (var symbol in symbols)
        {
            if (symbol.IsDocumentSymbol && symbol.DocumentSymbol != null)
                Nodes.Add(CreateNode(symbol.DocumentSymbol, editor.FullPath));
            else if (symbol.IsDocumentSymbolInformation && symbol.SymbolInformation != null)
            {
                var info = symbol.SymbolInformation;
                Nodes.Add(new VerilogSymbolNode(info.Name, info.Kind, info.Location.Uri.GetFileSystemPath(),
                    info.Location.Range));
            }
        }
    }

    [RelayCommand]
    public async Task NavigateAsync()
    {
        if (SelectedNode == null) return;
        if (await _mainDockService.OpenFileAsync(SelectedNode.FilePath) is not IEditor editor) return;

        var start = SelectedNode.Range.Start;
        var end = SelectedNode.Range.End;
        var startOffset = editor.CurrentDocument.GetOffset(start.Line + 1, start.Character + 1);
        var endOffset = editor.CurrentDocument.GetOffset(end.Line + 1, end.Character + 1);
        editor.Select(startOffset, Math.Max(endOffset - startOffset, 0));
    }

    private static VerilogSymbolNode CreateNode(DocumentSymbol symbol, string filePath)
    {
        var node = new VerilogSymbolNode(symbol.Name, symbol.Kind, filePath, symbol.SelectionRange);
        if (symbol.Children is not null)
            foreach (var child in symbol.Children)
                node.Children.Add(CreateNode(child, filePath));
        return node;
    }
}
