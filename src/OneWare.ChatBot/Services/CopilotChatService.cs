using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using OneWare.ChatBot.Models;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Services;

namespace OneWare.ChatBot.Services;

public sealed class CopilotChatService(
    ITerminalManagerService terminalManagerService,
    IProjectExplorerService projectExplorerService,
    IMainDockService dockService,
    IErrorService errorService,
    IPaths paths)
    : IChatService
{
    private CopilotClient? _client;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private CopilotSession? _session;
    private string? _currentModel;

    private IDisposable? _subscription;
    private readonly IProjectExplorerService _projectExplorerService = projectExplorerService;

    public string Name { get; } = "Copilot";

    public event EventHandler<ChatServiceMessageEvent>? MessageReceived;
    public event EventHandler<ChatServiceStatusEvent>? StatusChanged;

    public async Task<ModelModel[]> InitializeAsync()
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        await DisposeAsync();

        try
        {
            _client = new CopilotClient(new CopilotClientOptions()
            {
                Cwd = paths.ProjectsDirectory,
            });

            StatusChanged?.Invoke(this, new ChatServiceStatusEvent(false, $"Starting Copilot..."));

            await _client.StartAsync();

            var models = await _client.ListModelsAsync();

            StatusChanged?.Invoke(this, new ChatServiceStatusEvent(true, $"Copilot started"));

            return models.Select(x => new ModelModel()
            {
                Id = x.Id,
                Name = x.Name,
                Billing = $"{x.Billing?.Multiplier}x",
            }).ToArray();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new ChatServiceStatusEvent(false, "Copilot unavailable"));
            MessageReceived?.Invoke(this, new ChatServiceMessageEvent(ChatServiceMessageType.Error, ex.Message));

            return [];
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task InitializeSessionAsync(string model)
    {
        if (_client == null) return;

        await DisposeSessionAsync();

        StatusChanged?.Invoke(this, new ChatServiceStatusEvent(true, $"Connecting to {model}..."));

        var getActiveProject = AIFunctionFactory.Create(
            () => new
            {
                activeProject = _projectExplorerService.ActiveProject?.FullPath
            },
            "getActiveProject",
            "Returns the working directory of the active project. If no active project is enabled, it will return null."
        );

        var getOpenFiles = AIFunctionFactory.Create(
            () => new
            {
                openFiles = dockService.OpenFiles.Select(x => x.Key.FullPath).ToArray()
            },
            "getOpenFiles",
            """
            Returns the full paths of ALL files currently open in the IDE.
            This is the ONLY way to know which files are open.
            Do not assume or invent open files.
            """
        );

        var getOpenFile = AIFunctionFactory.Create(
            () => new
            {
                currentFile = dockService.CurrentDocument?.FullPath
            },
            "getFocusedFile",
            """
            Returns the full path of the currently focused editor file.
            This is the ONLY way to know which file is active.
            """
        );
        
        var getErrorsForFile = AIFunctionFactory.Create(
            ([Description("path of the file to get errors")]
                string path) => new
            {
                errorsForFile = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var errors = errorService.GetErrors();
                    var errorStrings = errors
                        .Where(x => x.File.FullPath.EqualPaths(path))
                        .Select(x => x.ToString()).ToArray();
                    return errorStrings;
                }) 
            },
            "getErrorsForFile",
            """
            Returns the LSP Errors for the specified path (if any)
            """
        );
        
        var getErrors = AIFunctionFactory.Create(
            () => new
            {
                errors = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var errors = errorService.GetErrors();
                    var errorStrings = errors
                        .Select(x => x.ToString()).ToArray();
                    return errorStrings;
                }) 
            },
            "getAllErrors",
            "Returns all the errors found by LSP"
        );

        var executeInTerminal = AIFunctionFactory.Create(
            (
                [Description("Shell command to execute")]
                string command,
                [Description("Working directory for execution")]
                string workDir
            ) => new
            {
                result = Dispatcher.UIThread.InvokeAsync(async () =>
                    await terminalManagerService.ExecuteInTerminalAsync(
                        command,
                        "Copilot",
                        workDir,
                        true,
                        TimeSpan.FromMinutes(1)))
            },
            "runTerminalCommand",
            """
            Executes a command in the user's visible terminal.
            This is the ONLY way to run commands.
            Do NOT simulate command execution or output.
            """
        );

        _session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Content = """
                          You are running inside an IDE called OneWare Studio. It supports opening multiple projects 

                          IMPORTANT RULES:
                          - THE CWD is not important since this App supports opening multiple projects in different locations. You can ask about the active project location with getActiveProject
                          - You DO NOT have access to files that are not open in the IDE (ask with getOpenFiles).
                          - You MUST NOT assume file contents, directory structure, or command output.
                          - You MUST use the provided tools to:
                            - discover open files
                            - determine the currently focused file
                            - execute terminal commands
                          - If a task requires file access or execution, you MUST call the appropriate tool.
                          - Never simulate terminal output.
                          - Never invent file paths or command results.
                          - If the user asks to edit something, start with the currently focused file (ask with getFocusedFile) (if not specified otherwise)
                          If a required tool is missing, ask the user.
                          """
            },
            Tools = [getActiveProject, getOpenFiles, getOpenFile, getErrorsForFile, getErrors, executeInTerminal]
        });

        StatusChanged?.Invoke(this, new ChatServiceStatusEvent(true, $"Connected"));

        _currentModel = model;
        _subscription = _session.On(HandleSessionEvent);
    }

    public async Task SendAsync(string model, string prompt)
    {
        if (_session == null || _currentModel != model)
        {
            await InitializeSessionAsync(model);
        }

        if (_session == null) return;
        await _session.SendAsync(new MessageOptions { Prompt = prompt }).ConfigureAwait(false);
    }

    public async Task AbortAsync()
    {
        if (_session == null) return;
        await _session.AbortAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeSessionAsync();
        if (_client != null)
        {
            await _client.StopAsync();
            await _client.DisposeAsync();
        }
    }

    private async Task DisposeSessionAsync()
    {
        _subscription?.Dispose();
        _subscription = null;

        if (_session != null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
            _currentModel = null;
        }
    }

    private void HandleSessionEvent(SessionEvent evt)
    {
        switch (evt)
        {
            case AssistantMessageDeltaEvent delta:
                MessageReceived?.Invoke(this,
                    new ChatServiceMessageEvent(ChatServiceMessageType.AssistantDelta, delta.Data.DeltaContent));
                break;
            case AssistantMessageEvent message:
                MessageReceived?.Invoke(this,
                    new ChatServiceMessageEvent(ChatServiceMessageType.AssistantMessage, message.Data.Content));
                break;
            case SessionErrorEvent error:
                MessageReceived?.Invoke(this,
                    new ChatServiceMessageEvent(ChatServiceMessageType.Error, error.Data.Message));
                break;
            case SessionIdleEvent:
                MessageReceived?.Invoke(this, new ChatServiceMessageEvent(ChatServiceMessageType.Idle));
                break;
        }
    }
}