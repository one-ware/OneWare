using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using Asmichi.ProcessManagement;
using Avalonia.Threading;
using Nerdbank.Streams;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.Essentials.LanguageService
{
    public abstract class LanguageServiceLsp(string name, string? workspace) : LanguageServiceBase(name, workspace)
    {
        private readonly Dictionary<ProgressToken, (ApplicationProcess, string)> _tokenRegister = new();
        private LanguageClient? Client { get; set; }
        
        private CancellationTokenSource? _cancellation;
        private IChildProcess? _process;
        protected string? Arguments { get; set; }
        protected string? ExecutablePath { get; set; }

        public override async Task ActivateAsync()
        {
            if (IsActivated) return;
            IsActivated = true;
            
            if (ExecutablePath == null)
            {
                ContainerLocator.Container.Resolve<ILogger>().Warning($"Tried to activate Language Server {Name} without executable!", new NotSupportedException(), false);
                return;
            }
            
            if ((RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
            {
                PlatformHelper.ChmodFile(ExecutablePath);
            }
            
            _cancellation = new CancellationTokenSource();
            
            if (ExecutablePath.StartsWith("wss://") || ExecutablePath.StartsWith("ws://"))
            {
                var websocket = new ClientWebSocket();
                try
                {
                    await websocket.ConnectAsync(new Uri(ExecutablePath), _cancellation.Token);

                    await InitAsync(websocket.UsePipeReader().AsStream(), websocket.UsePipeWriter().AsStream());
                    return;
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);

                    IsActivated = false;
                    return;
                }
            }
            else
            {
                if (!PlatformHelper.Exists(ExecutablePath))
                {
                    ContainerLocator.Container.Resolve<ILogger>()
                        ?.Warning($"{Name} language server not found! {ExecutablePath}");
                    return;
                }
                
                var argumentArray = Arguments != null ? 
                    Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries).ToArray() : Array.Empty<string>();
                
                var processStartInfo = new ChildProcessStartInfo(ExecutablePath, argumentArray)
                {
                    StdOutputRedirection = OutputRedirection.OutputPipe,
                    StdInputRedirection = InputRedirection.InputPipe,
                    StdErrorRedirection = OutputRedirection.ErrorPipe,
                };
                
                //_process.ErrorDataReceived +=
                //    (o, i) => ContainerLocator.Container.Resolve<ILogger>()?.Error(i.Data ?? "");

                try
                {
                    _process = ContainerLocator.Container.Resolve<IChildProcessService>().StartChildProcess(processStartInfo);
                    var reader = new StreamReader(_process.StandardError);
                    _ = Task.Run(() =>
                    {
                        while (_process.HasStandardError && !reader.EndOfStream && !_cancellation.IsCancellationRequested)
                        {
                            Console.WriteLine("ERR:" + reader.ReadToEnd());
                        }
                    }, _cancellation.Token);
                    
                    await InitAsync(_process.StandardOutput, _process.StandardInput);

                    await _process.WaitForExitAsync();

                    await DeactivateAsync();
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                    IsActivated = false;
                }
            }
        }

        public override async Task DeactivateAsync()
        {
            IsActivated = false;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Client == null) return;
                try
                {
                    Client.SendExit();

                    await Task.Delay(200);
                    Client = null;
                    IsLanguageServiceReady = false;
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }

                ContainerLocator.Container.Resolve<IErrorService>()?.Clear(Name);
            });
            await base.DeactivateAsync();
            _cancellation?.Cancel();
            _process?.Kill();
        }
        
        private async Task InitAsync(Stream input, Stream output, Action<LanguageClientOptions>? customOptions = null)
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
                    options.OnShowMessage(x => ContainerLocator.Container.Resolve<ILogger>()?.Log(x.Message, ConsoleColor.DarkCyan));
                    options.OnTelemetryEvent(x => { ContainerLocator.Container.Resolve<ILogger>()?.Log(x, ConsoleColor.Magenta); });

                    options.WithCapability(new TextSynchronizationCapability()
                    {
                        DidSave = true,
                        WillSave = false,
                        WillSaveWaitUntil = false
                    });
                    options.WithCapability(new HoverCapability
                    {
                        ContentFormat = new Container<MarkupKind>(MarkupKind.PlainText, MarkupKind.Markdown)
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
                    // options.WithCapability(new SemanticTokensCapability()
                    // {
                    //     OverlappingTokenSupport = false,
                    //     ServerCancelSupport = true,
                    //     Requests = new SemanticTokensCapabilityRequests()
                    //     {
                    //         Range = new Supports<SemanticTokensCapabilityRequestRange?>(false),
                    //         Full = new Supports<SemanticTokensCapabilityRequestFull?>(true)
                    //     },
                    //     TokenTypes = new Container<SemanticTokenType>(SemanticTokenType.Type, SemanticTokenType.Function, SemanticTokenType.Variable, SemanticTokenType.Class, SemanticTokenType.Keyword),
                    //     Formats = new Container<SemanticTokenFormat>(SemanticTokenFormat.Relative),
                    //     AugmentsSyntaxTokens = true,
                    //     MultilineTokenSupport = true
                    // });
                    // options.WithCapability(new DidChangeWatchedFilesCapability()
                    // {
                    //     
                    // });
                    options.WithCapability(new ReferenceCapability()
                    {
                        
                    });
                    options.WithCapability(new SignatureHelpCapability
                    {
                        SignatureInformation = new SignatureInformationCapabilityOptions
                        {
                            DocumentationFormat = new Container<MarkupKind>(MarkupKind.PlainText),
                            ParameterInformation = new SignatureParameterInformationCapabilityOptions()
                            {
                                LabelOffsetSupport = true
                            },
                            ActiveParameterSupport = true
                        },
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
                            CommitCharactersSupport = false,
                            DocumentationFormat = new Container<MarkupKind>(MarkupKind.PlainText),
                            SnippetSupport = true,
                            PreselectSupport = true,
                            InsertReplaceSupport = true,
                            InsertTextModeSupport = new CompletionItemInsertTextModeSupportCapabilityOptions()
                            {
                                ValueSet = new Container<InsertTextMode>(InsertTextMode.AdjustIndentation, InsertTextMode.AsIs)
                            },
                            ResolveAdditionalTextEditsSupport = true,
                            TagSupport = new Supports<CompletionItemTagSupportCapabilityOptions?>
                            {
                                Value = new CompletionItemTagSupportCapabilityOptions
                                {
                                    ValueSet = new Container<CompletionItemTag>(CompletionItemTag.Deprecated)
                                }
                            },
                            LabelDetailsSupport = true,
                        },
                        CompletionItemKind = new CompletionItemKindCapabilityOptions
                        {
                            ValueSet = new Container<CompletionItemKind>(
                                (CompletionItemKind[])Enum.GetValues(typeof(CompletionItemKind)))
                        },
                        ContextSupport = true,
                        InsertTextMode = InsertTextMode.AdjustIndentation,
                        DynamicRegistration = false
                    });
                    options.WithClientCapabilities(new ClientCapabilities
                    {
                        Workspace = new WorkspaceClientCapabilities
                        {
                            ApplyEdit = true,
                            Configuration = true,
                            DidChangeConfiguration = new DidChangeConfigurationCapability
                            {
                                DynamicRegistration = false
                            },
                            DidChangeWatchedFiles = new DidChangeWatchedFilesCapability
                            {
                                DynamicRegistration = false
                            },
                            ExecuteCommand = new ExecuteCommandCapability
                            {
                                DynamicRegistration = false
                            },
                            Symbol = new WorkspaceSymbolCapability
                            {
                                DynamicRegistration = false,
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
                    });

                    customOptions?.Invoke(options);
                }
            );

            var cancelToken = new CancellationToken();

            ContainerLocator.Container.Resolve<ILogger>()?.Log("Preinit finished " + Name, ConsoleColor.DarkCyan);

            try
            {
                await Client.Initialize(cancelToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error($"Initializing {Name} failed! {e.Message}", e);

                return;
            }

            ContainerLocator.Container.Resolve<ILogger>()?.Log("init finished " + Name, ConsoleColor.DarkCyan);

            IsLanguageServiceReady = true;
            await base.ActivateAsync();
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

        private void WriteLog(LogMessageParams log)
        {
            //ContainerLocator.Container.Resolve<ILogger>()?.Log("LS: " + log.Message);
            //MainDock.Output.WriteLine(Path.GetFileName(LanguageServerPath) + ": " + log.Type.ToString() + " " + log.Message);
            Debug.WriteLine(log.Message);
        }

        public void ReloadConfiguration()
        {
            Client?.DidChangeConfiguration(new DidChangeConfigurationParams());
        }

        public override void DidOpenTextDocument(string fullPath, string text)
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

        public override void DidSaveTextDocument(string fullPath, string text)
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

        public override void DidCloseTextDocument(string fullPath)
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
        /// Change watched file
        /// </summary>
        public virtual void DidChangeWatchedFile(FileChangeType type, string fullPath)
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
        public override void RefreshTextDocument(string fullPath, Container<TextDocumentContentChangeEvent> changes)
        {
            if (Client?.ServerSettings.Capabilities.TextDocumentSync?.Kind is not (TextDocumentSyncKind.Incremental
                or TextDocumentSyncKind.None)) return;
            
            _version++;

            Client?.DidChangeTextDocument(new DidChangeTextDocumentParams
            {
                TextDocument =
                    new OptionalVersionedTextDocumentIdentifier { Uri = fullPath, Version = _version},
                ContentChanges = changes
            });

        }
        
        public override void RefreshTextDocument(string fullPath, string newText)
        {
            if (Client?.ServerSettings.Capabilities.TextDocumentSync?.Kind is not TextDocumentSyncKind.Full) return;
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

        public override Task ExecuteCommandAsync(Command cmd)
        {
            if (Client?.ServerSettings.Capabilities.ExecuteCommandProvider == null) 
                return Task.CompletedTask;
            return Client.ExecuteCommand(cmd);
        }

        public override async Task<IEnumerable<SemanticToken>?> RequestSemanticTokensFullAsync(string fullPath)
        {
            if (Client?.ServerSettings.Capabilities.SemanticTokensProvider == null) return null;
            try
            {
                var semanticTokens = await Client.RequestSemanticTokensFull(new SemanticTokensParams()
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                });
                if (semanticTokens == null) return null;

                var legend = Client.ServerSettings.Capabilities.SemanticTokensProvider.Legend;
                var parsedTokens = SemanticTokenHelper.ParseSemanticTokens(semanticTokens.Data, legend);
                return parsedTokens;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }
        
        public override async Task<InlayHintContainer?> RequestInlayHintsAsync(string fullPath, Range range)
        {
            if (Client?.ServerSettings.Capabilities.InlayHintProvider == null) return null;
            try
            {
                var inlayHintContainer = await Client.RequestInlayHints(new InlayHintParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = fullPath
                    },
                    Range = range
                });
                return inlayHintContainer;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e, false);
            }

            return null;
        }
        
        public override async Task<CompletionList?> RequestCompletionAsync(string fullPath, Position pos, CompletionTriggerKind triggerKind, string? triggerChar)
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
                        TriggerCharacter = triggerChar,
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

        public override async Task<CompletionItem?> ResolveCompletionItemAsync(CompletionItem completionItem)
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

        public override async Task<IEnumerable<LocationOrLocationLink>?> RequestImplementationAsync(string fullPath,
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

        public override async Task<IEnumerable<LocationOrLocationLink>?> RequestTypeDefinitionAsync(string fullPath,
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

        public override async Task<IEnumerable<LocationOrLocationLink>?> RequestDefinitionAsync(string fullPath, Position pos)
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

        public override async Task<IEnumerable<LocationOrLocationLink>?> RequestDeclarationAsync(string fullPath, Position pos)
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

        public override async Task<CommandOrCodeActionContainer?> RequestCodeActionAsync(string fullPath, Range range, Diagnostic diagnostic)
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
                    Context = new CodeActionContext
                    {
                        Diagnostics = new Container<Diagnostic>(diagnostic)
                    }
                });

                return ca;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return null;
            }
        }

        public override async Task<WorkspaceEdit?> RequestRenameAsync(string fullPath, Position pos, string newName)
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

        public override async Task<RangeOrPlaceholderRange?> PrepareRenameAsync(string fullPath, Position pos)
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

        public override async Task<SignatureHelp?> RequestSignatureHelpAsync(string fullPath, Position pos, SignatureHelpTriggerKind triggerKind, string? triggerChar, bool isRetrigger, SignatureHelp? activeSignatureHelp)
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
                    Position = pos,
                    Context = new SignatureHelpContext()
                    {
                        TriggerCharacter = triggerChar,
                        TriggerKind = triggerKind,
                        IsRetrigger = isRetrigger,
                        ActiveSignatureHelp = activeSignatureHelp
                    }
                });
                return signatureHelp;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return null;
        }

        public override async Task<Container<FoldingRange>?> RequestFoldingsAsync(string fullPath)
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

        public override async Task<Hover?> RequestHoverAsync(string fullPath, Position pos)
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

        public override async Task<Container<ColorInformation>?> RequestDocumentColorAsync(string fullPath)
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

        public override async Task<DocumentHighlightContainer?> RequestDocumentHighlightAsync(string fullPath, Position pos)
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
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e, false);
            }

            return null;
        }

        public override async Task<Container<WorkspaceSymbol>?> RequestWorkspaceSymbolsAsync(string query)
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

        public override async Task<LocationContainer?> RequestReferencesAsync(string fullPath, Position pos)
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

        public override async Task<SymbolInformationOrDocumentSymbolContainer?> RequestSymbolsAsync(string fullPath)
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

        public override async Task<TextEditContainer?> RequestFormattingAsync(string fullPath)
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
        
        public override async Task<TextEditContainer?> RequestRangeFormattingAsync(string fullPath, Range range)
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
        
        #region Capability Check

        public override IEnumerable<string> GetSignatureHelpTriggerChars()
        {
            return Client?.ServerSettings.Capabilities.SignatureHelpProvider?.TriggerCharacters ?? [];
        }

        public override IEnumerable<string> GetSignatureHelpRetriggerChars()
        {
            return Client?.ServerSettings.Capabilities.SignatureHelpProvider?.RetriggerCharacters ?? [];
        }

        public override IEnumerable<string> GetCompletionTriggerChars()
        {
            return Client?.ServerSettings.Capabilities.CompletionProvider?.TriggerCharacters ?? [];
        }

        public override IEnumerable<string> GetCompletionCommitChars()
        {
            return Client?.ServerSettings.Capabilities.CompletionProvider?.AllCommitCharacters ?? [];
        }

        #endregion
    }
}