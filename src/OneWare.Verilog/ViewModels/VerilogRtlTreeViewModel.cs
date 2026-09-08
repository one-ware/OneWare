using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Verilog.Models;

namespace OneWare.Verilog.ViewModels;

public partial class VerilogRtlTreeViewModel : ExtendedTool
{
    public const string IconKey = "VsImageLib.Team16X";

    private readonly ILanguageManager _languageManager;
    private readonly IMainDockService _mainDockService;

    [ObservableProperty] private bool _isReverse;
    [ObservableProperty] private VerilogRtlTreeNode? _selectedNode;

    public VerilogRtlTreeViewModel(IMainDockService mainDockService, ILanguageManager languageManager)
        : base(IconKey)
    {
        _mainDockService = mainDockService;
        _languageManager = languageManager;
        Id = "VerilogRtlTree";
        Title = "RTL Tree";
    }

    public ObservableCollection<VerilogRtlTreeNode> Nodes { get; } = new();

    [RelayCommand]
    public Task LoadForwardAsync()
    {
        IsReverse = false;
        return RefreshAsync();
    }

    [RelayCommand]
    public Task LoadReverseAsync()
    {
        IsReverse = true;
        return RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        Nodes.Clear();
        if (_mainDockService.CurrentDocument is not IEditor editor) return;
        if (_languageManager.GetLanguageService(editor.FullPath) is not LanguageServiceVerilog service) return;

        var result = await service.ExecuteCommandAsync(CreateCommand(
            IsReverse ? "lazyverilog.rtlTreeReverse" : "lazyverilog.rtlTree",
            editor.FullPath));
        if (result is not JObject) return;

        var root = result.ToObject<VerilogRtlTreeNode>();
        if (root != null) Nodes.Add(root);
    }

    [RelayCommand]
    public async Task NavigateAsync()
    {
        if (SelectedNode == null || string.IsNullOrWhiteSpace(SelectedNode.File) || SelectedNode.Line <= 0) return;

        var path = SelectedNode.File.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            ? DocumentUri.From(SelectedNode.File).GetFileSystemPath()
            : SelectedNode.File;
        if (await _mainDockService.OpenFileAsync(path) is not IEditor editor) return;

        var line = editor.CurrentDocument.GetLineByNumber(Math.Min(SelectedNode.Line, editor.CurrentDocument.LineCount));
        editor.Select(Math.Min(line.Offset + SelectedNode.Column, line.EndOffset), 0);
    }

    private static Command CreateCommand(string name, string filePath)
    {
        return new Command
        {
            Title = name,
            Name = name,
            Arguments = new JArray(DocumentUri.FromFileSystemPath(filePath).ToString())
        };
    }
}
