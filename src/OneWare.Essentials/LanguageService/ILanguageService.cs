using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.ViewModels;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.Essentials.LanguageService;

public interface ILanguageService
{
    public bool IsActivated { get; }
    public bool IsLanguageServiceReady { get; }
    public string? Workspace { get; }
    
    public event EventHandler? LanguageServiceActivated;
    public event EventHandler? LanguageServiceDeactivated;
    
    public ITypeAssistance GetTypeAssistance(IEditor editor);
    
    public Task ActivateAsync();
    public Task DeactivateAsync();
    public Task RestartAsync();

    public void DidOpenTextDocument(string fullPath, string text);
    public void DidSaveTextDocument(string fullPath, string text);
    public void DidCloseTextDocument(string fullPath);

    public void RefreshTextDocument(string fullPath, Container<TextDocumentContentChangeEvent> changes);
    public void RefreshTextDocument(string fullPath, string newText);

    public IEnumerable<string> GetSignatureHelpTriggerChars();
    public IEnumerable<string> GetSignatureHelpRetriggerChars();
    public IEnumerable<string> GetCompletionTriggerChars();
    public IEnumerable<string> GetCompletionCommitChars();
    
    public Task<CompletionList?> RequestCompletionAsync(string fullPath, Position pos, CompletionTriggerKind triggerKind, string? triggerChar);
    public Task<CompletionItem?> ResolveCompletionItemAsync(CompletionItem completionItem);
    public Task<RangeOrPlaceholderRange?> PrepareRenameAsync(string fullPath, Position pos);
    public Task<WorkspaceEdit?> RequestRenameAsync(string fullPath, Position pos, string newName);

    public Task<SignatureHelp?> RequestSignatureHelpAsync(string fullPath, Position pos,
        SignatureHelpTriggerKind triggerKind, string? triggerChar, bool isRetrigger,
        SignatureHelp? activeSignatureHelp);
    
    public Task<CommandOrCodeActionContainer?> RequestCodeActionAsync(string fullPath, Range range, Diagnostic diagnostic);
    public Task<Container<FoldingRange>?> RequestFoldingsAsync(string fullPath);
    public Task<Hover?> RequestHoverAsync(string fullPath, Position pos);
    public Task<DocumentHighlightContainer?> RequestDocumentHighlightAsync(string fullPath, Position pos);
    public Task<Container<WorkspaceSymbol>?> RequestWorkspaceSymbolsAsync(string query);
    public Task<IEnumerable<LocationOrLocationLink>?> RequestTypeDefinitionAsync(string fullPath,
        Position pos);
    public Task<IEnumerable<LocationOrLocationLink>?> RequestDefinitionAsync(string fullPath, Position pos);
    public Task<LocationContainer?> RequestReferencesAsync(string fullPath, Position pos);
    public Task<IEnumerable<LocationOrLocationLink>?> RequestImplementationAsync(string fullPath,
        Position pos);
    public Task<IEnumerable<LocationOrLocationLink>?> RequestDeclarationAsync(string fullPath, Position pos);
    public Task<SymbolInformationOrDocumentSymbolContainer?> RequestSymbolsAsync(string fullPath);
    public Task<Container<ColorInformation>?> RequestDocumentColorAsync(string fullPath);
    public Task<IEnumerable<SemanticToken>?> RequestSemanticTokensFullAsync(string fullPath);
    public Task<InlayHintContainer?> RequestInlayHintsAsync(string fullPath, Range range);
    public Task<TextEditContainer?> RequestFormattingAsync(string fullPath);
    public Task<TextEditContainer?> RequestRangeFormattingAsync(string fullPath, Range range);
    public Task ExecuteCommandAsync(Command cmd);
    public Task<ApplyWorkspaceEditResponse> ApplyWorkspaceEditAsync(ApplyWorkspaceEditParams param);
    public Task ApplyWorkspaceEditAsync(WorkspaceEdit? param);
}