using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Python;

public sealed class PythonInterpreterPickerService(
    PythonInterpreterService interpreters,
    IWindowService windows,
    IMainDockService dock,
    IProjectExplorerService projects,
    IApplicationCommandService commands,
    ISettingsService settings,
    IPaths paths)
{
    private const string Automatic = "Automatic (workspace .venv/venv, global default, then PATH)";
    private const string Browse = "Browse...";
    private const string Refresh = "Refresh";
    private bool _initialized;
    private bool _isOpen;

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        interpreters.Initialize();
        var command = new AsyncRelayCommand(SelectInterpreterAsync);
        commands.RegisterCommand(new CommandApplicationCommand("Python: Select Interpreter", command)
        {
            Detail = "Select the Python interpreter for the active workspace"
        });
        windows.RegisterMenuItem("MainWindow_MainMenu/Code",
            new MenuItemModel("Python")
            {
                Header = "Python",
                Items =
                [
                    new MenuItemModel("SelectInterpreter")
                    {
                        Header = "Select Interpreter...",
                        Command = command
                    }
                ]
            });
    }

    public string? GetActiveWorkspace()
    {
        var document = dock.CurrentDocument;
        if (document != null && !string.IsNullOrWhiteSpace(document.FullPath) &&
            (string.Equals(document.Extension, ".py", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(document.Extension, ".pyi", StringComparison.OrdinalIgnoreCase)))
            return projects.GetRootFromFile(document.FullPath)?.RootFolderPath ??
                   Path.GetDirectoryName(document.FullPath);
        return projects.ActiveProject?.RootFolderPath;
    }

    public async Task SelectInterpreterAsync()
    {
        if (_isOpen) return;
        _isOpen = true;
        try
        {
            var workspace = GetActiveWorkspace();
            var owner = dock.CurrentDocument is { } document
                ? dock.GetWindowOwner(document)
                : dock.GetWindowOwner(projects);
            if (string.IsNullOrWhiteSpace(workspace))
            {
                await windows.ShowMessageAsync("Python interpreter",
                    "Open a Python file or select an active project to choose a workspace interpreter. " +
                    "The global default is available in Settings > Languages > Python.",
                    MessageBoxIcon.Info, owner);
                return;
            }
            workspace = PythonInterpreterService.NormalizeWorkspace(workspace);
            while (true)
            {
                interpreters.Refresh(workspace);
                var current = interpreters.GetInterpreter(workspace);
                var candidates = (await interpreters.DiscoverCandidatesAsync(workspace)).ToList();
                PythonInterpreterCandidate? invalidSelection = null;
                if (current.Source == PythonInterpreterSource.WorkspaceOverride && !current.IsResolved)
                {
                    invalidSelection = new PythonInterpreterCandidate(current.ExecutablePath ?? "",
                        "Current selection (missing or invalid)");
                    candidates.Insert(0, invalidSelection);
                }
                object[] options = [Automatic, .. candidates.Cast<object>(), Browse, Refresh];
                var explicitPath = interpreters.GetWorkspaceInterpreter(workspace);
                var pathComparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                object selected = candidates.FirstOrDefault(x =>
                    string.Equals(x.ExecutablePath, explicitPath, pathComparison)) as object ?? Automatic;
                var choice = await windows.ShowInputSelectAsync("Python: Select Interpreter",
                    $"Workspace: {workspace}\nCurrent: {current.Message}\n\n" +
                    "Automatic uses workspace .venv/venv, then the global default, then PATH. " +
                    "Python must be installed externally. Pyrefly project configuration may override " +
                    "this editor fallback. Cancel leaves the selection unchanged.",
                    current.IsResolved ? MessageBoxIcon.Info : MessageBoxIcon.Warning,
                    options, selected, owner);
                if (choice == null) return;
                if (Equals(choice, invalidSelection))
                {
                    await windows.ShowMessageAsync("Python interpreter", current.Message, MessageBoxIcon.Warning, owner);
                    return;
                }
                if (Equals(choice, Refresh)) continue;
                if (Equals(choice, Automatic))
                {
                    interpreters.SetWorkspaceInterpreter(workspace, null);
                    settings.Save(paths.SettingsPath);
                    return;
                }
                string? path;
                if (Equals(choice, Browse))
                {
                    if (owner == null)
                    {
                        await windows.ShowMessageAsync("Python interpreter",
                            "File browsing is not available in this window. " +
                            "Set the global default in Settings > Languages > Python.",
                            MessageBoxIcon.Info);
                        continue;
                    }
                    path = await StorageProviderHelper.SelectFileAsync(owner, "Select Python executable",
                        workspace, FilePickerFileTypes.All);
                    if (path == null) continue;
                }
                else if (choice is PythonInterpreterCandidate candidate)
                    path = candidate.ExecutablePath;
                else
                    return;

                interpreters.SetWorkspaceInterpreter(workspace, path);
                settings.Save(paths.SettingsPath);
                var result = interpreters.GetInterpreter(workspace);
                if (!result.IsResolved)
                    await windows.ShowMessageAsync("Python interpreter", result.Message, MessageBoxIcon.Warning, owner);
                return;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                         or NotSupportedException)
        {
            await windows.ShowMessageAsync("Python interpreter",
                "The interpreter selection could not be completed. Check that the workspace and executable " +
                "are accessible, or set the global default in Settings > Languages > Python.",
                MessageBoxIcon.Warning);
        }
        finally
        {
            _isOpen = false;
        }
    }
}
