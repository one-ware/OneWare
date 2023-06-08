using System.Diagnostics;
using Avalonia.Threading;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OneWare.Shared.Enums;
using OneWare.Shared.Extensions;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Ioc;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using TextDocument = AvaloniaEdit.Document.TextDocument;

namespace OneWare.Shared.LanguageService
{
    public abstract class LanguageServiceBase
    {
        private readonly Dictionary<ProgressToken, (ApplicationProcess, string)> _tokenRegister = new();
        public LanguageClient? Client { get; private set; }
        public bool IsActivated { get; protected set; }
        public string Name { get; }
        public string? Workspace { get; }
        public bool IsLanguageServiceReady { get; private set; }
        public event EventHandler? LanguageServiceActivated;
        public event EventHandler? LanguageServiceDeactivated;

        public LanguageServiceBase(string name, string? workspace = null)
        {
            Name = name;
            Workspace = workspace;
        }

        public abstract Task ActivateAsync();

        public virtual async Task DeactivateAsync()
        {
            IsActivated = false;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Client == null) return;
                try
                {
                    Client.SendExit();

                    await Task.Delay(100);
                    Client = null;
                    IsLanguageServiceReady = false;
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }

                ContainerLocator.Container.Resolve<IErrorService>()?.Clear(Name);
                LanguageServiceDeactivated?.Invoke(this, EventArgs.Empty);
            });
        }

        public virtual async Task RestartAsync()
        {
            await DeactivateAsync();
            await ActivateAsync();
        }

        /// <summary>
        ///     After Activate
        /// </summary>
        protected async Task InitAsync(Stream input, Stream output, Action<LanguageClientOptions>? customOptions = null)
        {
            Client = LanguageClient.PreInit(
                options =>
                {
                    options.WithClientInfo(new ClientInfo { Name = "OneWare.Core" });
                    options.WithInput(input).WithOutput(output);
                    if (Workspace != null)
                    {
                        options.WithRootPath(Workspace);
                        options.WithWorkspaceFolder(new WorkspaceFolder { Name = Workspace, Uri = Workspace });
                    }
                    options.WithMaximumRequestTimeout(TimeSpan.FromMilliseconds(500));
                    options.OnLogMessage(WriteLog);
                    options.OnWorkDoneProgressCreate(CreateWorkDoneProgress);
                    options.OnProgress(OnProgress);
                    options.OnLogTrace(x => ContainerLocator.Container.Resolve<ILogger>()?.Log(x.Message, ConsoleColor.Red));
                    options.OnPublishDiagnostics(PublishDiag);
                    options.OnApplyWorkspaceEdit(ApplyWorkspaceEditAsync);
                    options.EnableDynamicRegistration();
                    options.OnShowMessage(x => ContainerLocator.Container.Resolve<ILogger>()?.Log(x.Message, ConsoleColor.Magenta));
                    options.OnTelemetryEvent(x => { ContainerLocator.Container.Resolve<ILogger>()?.Log(x, ConsoleColor.Magenta); });
                    
                    options.WithCapability(new SynchronizationCapability
                    {
                        DidSave = true,
                        WillSave = true,
                        WillSaveWaitUntil = true
                    });
                    options.WithCapability(new HoverCapability
                    {
                        ContentFormat = new Container<MarkupKind>(MarkupKind.PlainText)
                    });
                    options.WithCapability(new PublishDiagnosticsCapability
                    {
                        RelatedInformation = false
                    });
                    options.WithCapability(new TypeDefinitionCapability
                    {
                        LinkSupport = false
                    });
                    options.WithCapability(new ImplementationCapability
                    {
                        LinkSupport = false
                    });
                    options.WithCapability(new DidChangeWatchedFilesCapability());
                    options.WithCapability(new ReferenceCapability());
                    options.WithCapability(new SignatureHelpCapability
                    {
                        SignatureInformation = new SignatureInformationCapabilityOptions
                            { DocumentationFormat = new Container<MarkupKind>(MarkupKind.PlainText) },
                        ContextSupport = true
                    });
                    options.WithCapability(new CodeActionCapability
                    {
                        IsPreferredSupport = true,
                        CodeActionLiteralSupport = new CodeActionLiteralSupportOptions
                        {
                            CodeActionKind = new CodeActionKindCapabilityOptions
                            {
                                ValueSet = new Container<CodeActionKind>(CodeActionKind.QuickFix, CodeActionKind.Empty,
                                    CodeActionKind.Source, CodeActionKind.SourceOrganizeImports,
                                    CodeActionKind.QuickFix, CodeActionKind.Refactor)
                            }
                        }
                    });
                    options.WithCapability(new RenameCapability
                    {
                        PrepareSupport = true
                    });
                    options.WithCapability(new CompletionCapability
                    {
                        CompletionItem = new CompletionItemCapabilityOptions
                        {
                            CommitCharactersSupport = true,
                            DocumentationFormat = new Container<MarkupKind>(MarkupKind.PlainText),
                            SnippetSupport = true,
                            PreselectSupport = true,
                            TagSupport = new Supports<CompletionItemTagSupportCapabilityOptions?>
                            {
                                Value = new CompletionItemTagSupportCapabilityOptions
                                {
                                    ValueSet = new Container<CompletionItemTag>(CompletionItemTag.Deprecated)
                                }
                            }
                        },
                        CompletionItemKind = new CompletionItemKindCapabilityOptions
                        {
                            ValueSet = new Container<CompletionItemKind>(
                                (CompletionItemKind[])Enum.GetValues(typeof(CompletionItemKind)))
                        },
                        ContextSupport = true,
                        DynamicRegistration = true
                    });
                    options.WithClientCapabilities(new ClientCapabilities
                    {
                        Workspace = new WorkspaceClientCapabilities
                        {
                            ApplyEdit = true,
                            Configuration = true,
                            DidChangeConfiguration =
                                new DidChangeConfigurationCapability { DynamicRegistration = true },
                            DidChangeWatchedFiles = new DidChangeWatchedFilesCapability { DynamicRegistration = true },
                            ExecuteCommand = new ExecuteCommandCapability { DynamicRegistration = true },
                            Symbol = new WorkspaceSymbolCapability
                            {
                                DynamicRegistration = true,
                                SymbolKind = new SymbolKindCapabilityOptions
                                {
                                    ValueSet = new Container<SymbolKind>(
                                        (SymbolKind[])Enum.GetValues(typeof(SymbolKind)))
                                }
                            },
                            WorkspaceEdit = new WorkspaceEditCapability { DocumentChanges = true },
                            WorkspaceFolders = true
                        },
                        Window = new WindowClientCapabilities(),
                        TextDocument = new TextDocumentClientCapabilities
                        {
                            Synchronization = new SynchronizationCapability
                                { DidSave = true, WillSave = true, WillSaveWaitUntil = true }
                        }
                    });

                    customOptions?.Invoke(options);
                }
            );

            var cancelToken = new CancellationToken();

            ContainerLocator.Container.Resolve<ILogger>()?.Log("Preinit finished " + Name, ConsoleColor.Magenta);

            try
            {
                await Client.Initialize(cancelToken).ConfigureAwait(false);
                //client.ProgressManager.
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error($"Initializing {Name} failed! {e.Message}", e);

                return;
            }

            ContainerLocator.Container.Resolve<ILogger>()?.Log("init finished " + Name, ConsoleColor.Magenta);

            IsLanguageServiceReady = true;
            Dispatcher.UIThread.Post(() => LanguageServiceActivated?.Invoke(this, EventArgs.Empty));
        }

        private void OnProgress(ProgressParams obj)
        {
            var kind = obj.Value["kind"]?.ToString();

            switch (kind)
            {
                case "begin":
                    var title = obj.Value["title"]?.ToString();
                    //var state = Active.AddState(title, AppState.Idle);
                    //if(!_tokenRegister.ContainsKey(obj.Token)) _tokenRegister.Add(obj.Token, (state, Name + ": " + title));
                    break;
                case "report":
                    int.TryParse(obj.Value["percentage"]?.ToString(), out var percentage);
                    if (!_tokenRegister.ContainsKey(obj.Token)) return;
                    _tokenRegister[obj.Token].Item1.StatusMessage =
                        _tokenRegister[obj.Token].Item2 + " " + percentage + "%";
                    break;
                case "end":
                    //if(_tokenRegister.ContainsKey(obj.Token)) Active.RemoveState(_tokenRegister[obj.Token].Item1);
                    //_tokenRegister.Remove(obj.Token);
                    break;
            }
        }

        private void CreateWorkDoneProgress(WorkDoneProgressCreateParams arg1, CancellationToken arg2)
        {
        }

        public void ProcessNotification(object notification)
        {
        }

        public void WriteLog(LogMessageParams log)
        {
            //ContainerLocator.Container.Resolve<ILogger>()?.Log("LS: " + log.Message);
            //MainDock.Output.WriteLine(Path.GetFileName(LanguageServerPath) + ": " + log.Type.ToString() + " " + log.Message);
        }

        public void ReloadConfiguration()
        {
            Client?.DidChangeConfiguration(new DidChangeConfigurationParams());
        }

        public void DidOpenTextDocument(string fullPath, string text)
        {
            Client?.DidOpenTextDocument(new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Text = text,
                    Uri = fullPath,
                    LanguageId = Path.GetExtension(fullPath),
                    Version = _version
                }
            });
        }

        public void DidSaveTextDocument(string fullPath, string text)
        {
            Client?.DidSaveTextDocument(new DidSaveTextDocumentParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = fullPath
                },
                Text = text
            });
        }

        public void DidCloseTextDocument(string fullPath)
        {
            Client?.DidCloseTextDocument(new DidCloseTextDocumentParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = fullPath
                }
            });
        }

        /// <summary>
        ///     Change watched file
        /// </summary>
        public void DidChangeWatchedFile(FileChangeType type, string fullPath)
        {
            Client?.DidChangeWatchedFiles(new DidChangeWatchedFilesParams
            {
                Changes = new Container<FileEvent>(
                    new FileEvent
                    {
                        Type = type,
                        Uri = fullPath
                    }
                )
            });
        }

        private int _version = 0;
        /// <summary>
        ///     Refresh contents of already opened document
        /// </summary>
        public virtual void RefreshTextDocument(string fullPath, Container<TextDocumentContentChangeEvent> changes)
        {
            _version++;

            Client?.DidChangeTextDocument(new DidChangeTextDocumentParams
            {
                TextDocument =
                    new OptionalVersionedTextDocumentIdentifier { Uri = fullPath, Version = _version},
                ContentChanges = changes
            });
        }
        
        public virtual void RefreshTextDocument(string fullPath, string newText)
        {
            _version++;

            Client?.DidChangeTextDocument(new DidChangeTextDocumentParams
            {
                TextDocument =
                    new OptionalVersionedTextDocumentIdentifier { Uri = fullPath , Version = _version},
                ContentChanges = new Container<TextDocumentContentChangeEvent>(new TextDocumentContentChangeEvent()
                {
                    Text = newText
                })
            });
        }

        public async Task ExecuteCommandAsync(Command cmd)
        {
            if (Client?.ServerSettings.Capabilities.ExecuteCommandProvider == null) return;
            await Client.ExecuteCommand(cmd);
        }

        public virtual async Task<CompletionList?> RequestCompletionAsync(string fullPath, Position pos, string triggerChar,
            CompletionTriggerKind triggerKind)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000);
            if (Client?.ServerSettings.Capabilities.CompletionProvider == null) return null;
            try
            {
                var completion = await Client.RequestCompletion(new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Context = new CompletionContext
                    {
                        TriggerKind = triggerKind,
                        TriggerCharacter = triggerChar
                    },
                    Position = pos
                }, cts.Token);
                return completion;
            }
            catch (Exception e)
            {
                if(e is TaskCanceledException) ContainerLocator.Container.Resolve<ILogger>()?.Warning("Completion timed out!");
                else ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public async Task<CompletionItem?> ResolveCompletionItemAsync(CompletionItem completionItem)
        {
            if (Client == null || Client.ServerSettings.Capabilities.CompletionProvider?.ResolveProvider == false)
                return null;
            try
            {
                var completion = await Client.ResolveCompletion(completionItem);
                return completion;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public async Task<IEnumerable<LocationOrLocationLink>?> RequestImplementationAsync(string fullPath,
            Position pos)
        {
            if (Client == null || Client.ServerSettings.Capabilities.ImplementationProvider == null) return null;
            try
            {
                var implementation = await Client.RequestImplementation(new ImplementationParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Position = pos
                });
                return implementation;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return null;
            }
        }

        public async Task<IEnumerable<LocationOrLocationLink>?> RequestTypeDefinitionAsync(string fullPath,
            Position pos)
        {
            if (Client == null || Client.ServerSettings.Capabilities.TypeDefinitionProvider == null) return null;
            try
            {
                var typeDef = await Client.RequestTypeDefinition(new TypeDefinitionParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Position = pos
                });
                return typeDef;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return null;
            }
        }

        public virtual async Task<IEnumerable<LocationOrLocationLink>?> RequestDefinitionAsync(string fullPath, Position pos)
        {
            if (Client == null || Client.ServerSettings.Capabilities.DefinitionProvider == null) return null;
            try
            {
                var def = await Client.RequestDefinition(new DefinitionParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Position = pos
                });
                return def;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return null;
            }
        }

        public async Task<IEnumerable<LocationOrLocationLink>?> RequestDeclarationAsync(string fullPath, Position pos)
        {
            if (Client == null || Client.ServerSettings.Capabilities.DeclarationProvider == null) return null;
            try
            {
                var dec = await Client.RequestDeclaration(new DeclarationParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Position = pos
                });
                return dec;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return null;
            }
        }

        public async Task<CommandOrCodeActionContainer?> RequestCodeActionAsync(string fullPath, Range range)
        {
            if (Client == null || Client.ServerSettings.Capabilities.CodeActionProvider == null) return null;
            try
            {
                var ca = await Client.RequestCodeAction(new CodeActionParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Range = range,
                    // Context = new CodeActionContext
                    // {
                    //     Diagnostics = file.Diagnostics TODO
                    // }
                });

                return ca;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return null;
            }
        }

        public async Task<WorkspaceEdit?> RequestRenameAsync(string fullPath, Position pos, string newName)
        {
            if (Client == null || Client.ServerSettings.Capabilities.RenameProvider == null) return null;
            try
            {
                var ca = await Client.RequestRename(new RenameParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Position = pos,
                    NewName = newName
                });

                return ca;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return null;
            }
        }

        public async Task<RangeOrPlaceholderRange?> PrepareRenameAsync(string fullPath, Position pos)
        {
            if (Client == null || Client.ServerSettings.Capabilities.RenameProvider == null) return null;
            try
            {
                var ca = await Client.PrepareRename(new PrepareRenameParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Position = pos
                });

                return ca;
            }
            catch
            {
                //ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e); Enable once bug fixed
                return null;
            }
        }

        public virtual async Task<SignatureHelp?> RequestSignatureHelpAsync(string fullPath, Position pos)
        {
            if (Client?.ServerSettings.Capabilities.SignatureHelpProvider == null) return null;
            try
            {
                var signatureHelp = await Client.RequestSignatureHelp(new SignatureHelpParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Position = pos
                });
                return signatureHelp;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public async Task<Container<FoldingRange>?> RequestFoldingsAsync(string fullPath)
        {
            if (Client == null || Client.ServerSettings.Capabilities.FoldingRangeProvider == null) return null;
            try
            {
                var foldings = await Client.RequestFoldingRange(new FoldingRangeRequestParam
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    }
                });
                return foldings;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public virtual async Task<Hover?> RequestHoverAsync(string fullPath, Position pos)
        {
            if (Client == null || Client.ServerSettings.Capabilities.HoverProvider == null) return null;
            try
            {
                var hover = await Client.RequestHover(new HoverParams
                {
                    Position = pos,
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    }
                });

                return hover;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public async Task<Container<ColorInformation>?> RequestDocumentColorAsync(string fullPath)
        {
            if (Client == null || Client.ServerSettings.Capabilities.ColorProvider == null) return null;
            try
            {
                var color = await Client.RequestDocumentColor(new DocumentColorParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    }
                });

                return color;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public async Task<DocumentHighlightContainer?> RequestDocumentHighlightAsync(string fullPath, Position pos)
        {
            if (Client == null || Client.ServerSettings.Capabilities.DocumentHighlightProvider == null) return null;
            try
            {
                var highlight = await Client.RequestDocumentHighlight(new DocumentHighlightParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Position = pos
                });

                return highlight;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public async Task<Container<SymbolInformation>?> RequestWorkspaceSymbolsAsync(string query)
        {
            if (Client == null || Client.ServerSettings.Capabilities.WorkspaceSymbolProvider == null) return null;
            try
            {
                var refs = await Client.RequestWorkspaceSymbols(new WorkspaceSymbolParams
                {
                    Query = query
                });

                return refs;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public async Task<LocationContainer?> RequestReferencesAsync(string fullPath, Position pos)
        {
            if (Client == null || Client.ServerSettings.Capabilities.ReferencesProvider == null) return null;
            try
            {
                var refs = await Client.RequestReferences(new ReferenceParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Position = pos
                });

                return refs;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public async Task<SymbolInformationOrDocumentSymbolContainer?> RequestSymbolsAsync(string fullPath)
        {
            if (Client == null || Client.ServerSettings.Capabilities.DocumentSymbolProvider == null) return null;
            try
            {
                var refs = await Client.RequestDocumentSymbol(new DocumentSymbolParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath,
                    }
                });

                return refs;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public virtual async Task<TextEditContainer?> RequestFormattingAsync(string fullPath)
        {
            if (Client == null || Client.ServerSettings.Capabilities.DocumentFormattingProvider == null) return null;
            var formatting = await Client.RequestDocumentFormatting(new DocumentFormattingParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = fullPath
                }
            });

            return formatting;
        }

        public virtual async Task<TextEditContainer?> RequestRangeFormattingAsync(string fullPath, Range range)
        {
            if (Client == null || Client.ServerSettings.Capabilities.DocumentFormattingProvider == null) return null;
            var formatting = await Client.RequestDocumentRangeFormatting(new DocumentRangeFormattingParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = fullPath
                },
                Range = range
            });

            return formatting;
        }


        public async Task<ApplyWorkspaceEditResponse> ApplyWorkspaceEditAsync(ApplyWorkspaceEditParams param)
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
        
        public async Task ApplyWorkspaceEditAsync(WorkspaceEdit? param)
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

        public void ApplyContainer(string path, IEnumerable<TextEdit> con)
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

        public static void ApplyContainer(TextDocument doc, IEnumerable<TextEdit> con, bool beginUpdate = true)
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

        public void PublishDiag(PublishDiagnosticsParams pdp)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var file = ContainerLocator.Container.Resolve<IDockService>().OpenFiles
                    .FirstOrDefault(x => x.Key.FullPath.EqualPaths(pdp.Uri.GetFileSystemPath())).Key;
                file ??= ContainerLocator.Container.Resolve<IProjectService>().Search(pdp.Uri.GetFileSystemPath()) as IFile;
                file ??= ContainerLocator.Container.Resolve<IProjectService>().GetTemporaryFile(pdp.Uri.GetFileSystemPath());
                ContainerLocator.Container.Resolve<IErrorService>().RefreshErrors(ConvertErrors(pdp, file).ToList(), Name, file);
                //file.Diagnostics = pdp.Diagnostics;
            }, DispatcherPriority.Background);
        }

        public virtual IEnumerable<ErrorListItemModel> ConvertErrors(PublishDiagnosticsParams pdp, IFile file)
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

                yield return new ErrorListItemModel(p.Message, errorType, file, Name, p.Range.Start.Line+1,
                    p.Range.Start.Character+1, p.Range.End.Line+1, p.Range.End.Character+1)
                {
                    Code = p.Code?.String ?? p.Code?.Long.ToString() ?? "",
                };
            }
        }
    }
}