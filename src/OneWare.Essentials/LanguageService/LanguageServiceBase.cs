using Avalonia.Threading;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using IFile = OneWare.Essentials.Models.IFile;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using TextDocument = AvaloniaEdit.Document.TextDocument;

namespace OneWare.Essentials.LanguageService;

public abstract class LanguageServiceBase : ILanguageService
{
    public bool IsActivated { get; protected set; }
    public string Name { get; }
    public string? Workspace { get; }
    public bool IsLanguageServiceReady { get; protected set; }
    public event EventHandler? LanguageServiceActivated;
    public event EventHandler? LanguageServiceDeactivated;

    protected LanguageServiceBase(string name, string? workspace = null)
    {
        Name = name;
        Workspace = workspace;
    }

    public abstract ITypeAssistance GetTypeAssistance(IEditor editor);

    public virtual Task ActivateAsync()
    {
        Dispatcher.UIThread.Post(() => LanguageServiceActivated?.Invoke(this, EventArgs.Empty));
        return Task.CompletedTask;
    }

    public virtual Task DeactivateAsync()
    {
        Dispatcher.UIThread.Post(() => LanguageServiceDeactivated?.Invoke(this, EventArgs.Empty));
        return Task.CompletedTask;
    }

    public virtual async Task RestartAsync()
    {
        await DeactivateAsync();
        await Task.Delay(100);
        await ActivateAsync();
    }

    public virtual void DidOpenTextDocument(string fullPath, string text)
    {
    }

    public virtual void DidSaveTextDocument(string fullPath, string text)
    {
    }

    public virtual void DidCloseTextDocument(string fullPath)
    {
    }

    public virtual void RefreshTextDocument(string fullPath, Container<TextDocumentContentChangeEvent> changes)
    {
    }

    public virtual void RefreshTextDocument(string fullPath, string newText)
    {
    }

    public virtual Task<CompletionList?> RequestCompletionAsync(string fullPath, Position pos,
        CompletionTriggerKind triggerKind, string? triggerChar)
    {
        return Task.FromResult<CompletionList?>(null);
    }

    public virtual Task<CompletionItem?> ResolveCompletionItemAsync(CompletionItem completionItem)
    {
        return Task.FromResult<CompletionItem?>(null);
    }

    public virtual Task<RangeOrPlaceholderRange?> PrepareRenameAsync(string fullPath, Position pos)
    {
        return Task.FromResult<RangeOrPlaceholderRange?>(null);
    }

    public virtual Task<WorkspaceEdit?> RequestRenameAsync(string fullPath, Position pos, string newName)
    {
        return Task.FromResult<WorkspaceEdit?>(null);
    }

    public virtual Task<SignatureHelp?> RequestSignatureHelpAsync(string fullPath, Position pos,
        SignatureHelpTriggerKind triggerKind, string? triggerChar, bool isRetrigger, SignatureHelp? activeSignatureHelp)
    {
        return Task.FromResult<SignatureHelp?>(null);
    }

    public virtual Task<CommandOrCodeActionContainer?> RequestCodeActionAsync(string fullPath, Range range,
        Diagnostic diagnostic)
    {
        return Task.FromResult<CommandOrCodeActionContainer?>(null);
    }

    public virtual Task<Container<FoldingRange>?> RequestFoldingsAsync(string fullPath)
    {
        return Task.FromResult<Container<FoldingRange>?>(null);
    }

    public virtual Task<Hover?> RequestHoverAsync(string fullPath, Position pos)
    {
        return Task.FromResult<Hover?>(null);
    }

    public virtual Task<DocumentHighlightContainer?> RequestDocumentHighlightAsync(string fullPath, Position pos)
    {
        return Task.FromResult<DocumentHighlightContainer?>(null);
    }

    public virtual Task<Container<WorkspaceSymbol>?> RequestWorkspaceSymbolsAsync(string query)
    {
        return Task.FromResult<Container<WorkspaceSymbol>?>(null);
    }

    public virtual Task<IEnumerable<LocationOrLocationLink>?> RequestTypeDefinitionAsync(string fullPath, Position pos)
    {
        return Task.FromResult<IEnumerable<LocationOrLocationLink>?>(null);
    }

    public virtual Task<IEnumerable<LocationOrLocationLink>?> RequestDefinitionAsync(string fullPath, Position pos)
    {
        return Task.FromResult<IEnumerable<LocationOrLocationLink>?>(null);
    }

    public virtual Task<LocationContainer?> RequestReferencesAsync(string fullPath, Position pos)
    {
        return Task.FromResult<LocationContainer?>(null);
    }

    public virtual Task<IEnumerable<LocationOrLocationLink>?> RequestImplementationAsync(string fullPath, Position pos)
    {
        return Task.FromResult<IEnumerable<LocationOrLocationLink>?>(null);
    }

    public virtual Task<IEnumerable<LocationOrLocationLink>?> RequestDeclarationAsync(string fullPath, Position pos)
    {
        return Task.FromResult<IEnumerable<LocationOrLocationLink>?>(null);
    }

    public virtual Task<SymbolInformationOrDocumentSymbolContainer?> RequestSymbolsAsync(string fullPath)
    {
        return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(null);
    }

    public virtual Task<Container<ColorInformation>?> RequestDocumentColorAsync(string fullPath)
    {
        return Task.FromResult<Container<ColorInformation>?>(null);
    }

    public virtual Task<IEnumerable<SemanticToken>?> RequestSemanticTokensFullAsync(string fullPath)     
    {
        return Task.FromResult<IEnumerable<SemanticToken>?>(null);
    }

    public virtual Task<InlayHintContainer?> RequestInlayHintsAsync(string fullPath, Range range)
    {
        return Task.FromResult<InlayHintContainer?>(null);
    }

    public virtual Task<TextEditContainer?> RequestFormattingAsync(string fullPath)
    {
        return Task.FromResult<TextEditContainer?>(null);
    }

    public virtual Task<TextEditContainer?> RequestRangeFormattingAsync(string fullPath, Range range)
    {
        return Task.FromResult<TextEditContainer?>(null);
    }

    public virtual Task ExecuteCommandAsync(Command cmd)
    {
        return Task.CompletedTask;
    }

    public virtual async Task<ApplyWorkspaceEditResponse> ApplyWorkspaceEditAsync(ApplyWorkspaceEditParams param)
    {
        if (param.Edit.Changes != null)
            foreach (var docChanges in param.Edit.Changes.Reverse())
            {
                var path = docChanges.Key.GetFileSystemPath();

                await Dispatcher.UIThread.InvokeAsync(() => { ApplyContainer(path, docChanges.Value); });
            }
        else if (param.Edit.DocumentChanges is not null)
            foreach (var docChanges in param.Edit.DocumentChanges.Reverse())
                if (docChanges.IsTextDocumentEdit && docChanges.TextDocumentEdit != null)
                {
                    var path = docChanges.TextDocumentEdit.TextDocument.Uri.GetFileSystemPath();

                    ApplyContainer(path, docChanges.TextDocumentEdit.Edits.AsEnumerable());
                }

        return new ApplyWorkspaceEditResponse { Applied = true };
    }

    public virtual async Task ApplyWorkspaceEditAsync(WorkspaceEdit? param)
    {
        if (param == null) return;

        if (param.Changes != null)
            foreach (var docChanges in param.Changes.Reverse())
            {
                var path = docChanges.Key.GetFileSystemPath();

                await Dispatcher.UIThread.InvokeAsync(() => { ApplyContainer(path, docChanges.Value); });
            }
        else if (param.DocumentChanges is not null)
            foreach (var docChanges in param.DocumentChanges.Reverse())
                if (docChanges.IsTextDocumentEdit && docChanges.TextDocumentEdit != null)
                {
                    var path = docChanges.TextDocumentEdit.TextDocument.Uri.GetFileSystemPath();

                    ApplyContainer(path, docChanges.TextDocumentEdit.Edits.AsEnumerable());
                }
    }

    public virtual void ApplyContainer(string path, IEnumerable<TextEdit> con)
    {
        var openDoc =
            ContainerLocator.Container.Resolve<IDockService>().OpenFiles
                .FirstOrDefault(x => x.Key.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)).Value as IEditor;

        try
        {
            if (openDoc != null)
            {
                ApplyContainer(openDoc.CurrentDocument, con);
            }
            else
            {
                var text = File.ReadAllText(path);
                var doc = new TextDocument(text);
                ApplyContainer(doc, con);
                File.WriteAllText(path, doc.Text);
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    private static void ApplyContainer(TextDocument doc, IEnumerable<TextEdit> con, bool beginUpdate = true)
    {
        if (beginUpdate) doc.BeginUpdate();
        try
        {
            foreach (var c in con.Reverse())
            {
                var sOff = doc.GetOffsetFromPosition(c.Range.Start) - 1;
                var eOff = doc.GetOffsetFromPosition(c.Range.End) - 1;

                doc.Replace(sOff, eOff - sOff, c.NewText);
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
        finally
        {
            if (beginUpdate) doc.EndUpdate();
        }
    }

    protected virtual void PublishDiag(PublishDiagnosticsParams pdp)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var file = ContainerLocator.Container.Resolve<IDockService>().OpenFiles
                .FirstOrDefault(x => x.Key.FullPath.EqualPaths(pdp.Uri.GetFileSystemPath())).Key;
            file ??=
                ContainerLocator.Container.Resolve<IProjectExplorerService>()
                    .SearchFullPath(pdp.Uri.GetFileSystemPath()) as IFile;
            file ??= ContainerLocator.Container.Resolve<IProjectExplorerService>()
                .GetTemporaryFile(pdp.Uri.GetFileSystemPath());
            ContainerLocator.Container.Resolve<IErrorService>()
                .RefreshErrors(ConvertErrors(pdp, file).ToList(), Name, file);
            //file.Diagnostics = pdp.Diagnostics;
        }, DispatcherPriority.Background);
    }

    protected virtual IEnumerable<ErrorListItem> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
    {
        foreach (var p in pdp.Diagnostics)
        {
            var errorType = ErrorType.Hint;
            if (p.Severity.HasValue)
                errorType = p.Severity.Value switch
                {
                    DiagnosticSeverity.Error => ErrorType.Error,
                    DiagnosticSeverity.Warning => ErrorType.Warning,
                    _ => ErrorType.Hint
                };

            yield return new ErrorListItem(p.Message, errorType, file, Name, p.Range.Start.Line + 1,
                p.Range.Start.Character + 1, p.Range.End.Line + 1, p.Range.End.Character + 1,
                p.Code?.String ?? p.Code?.Long.ToString() ?? "", p);
        }
    }

    public virtual IEnumerable<string> GetSignatureHelpTriggerChars()
    {
        return [];
    }

    public virtual IEnumerable<string> GetSignatureHelpRetriggerChars()
    {
        return [];
    }

    public virtual IEnumerable<string> GetCompletionTriggerChars()
    {
        return [];
    }

    public virtual IEnumerable<string> GetCompletionCommitChars()
    {
        return [];
    }
}