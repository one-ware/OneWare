using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Verilog.Services;

public class LazyVerilogCommandService(
    IMainDockService mainDockService,
    ILanguageManager languageManager,
    IWindowService windowService,
    IErrorService errorService,
    IProjectExplorerService projectExplorerService)
{
    public bool CanExecute =>
        mainDockService.CurrentDocument is IEditor editor &&
        languageManager.GetLanguageService(editor.FullPath) is LanguageServiceVerilog { IsLanguageServiceReady: true };

    public Task FormatAsync()
    {
        return ExecuteAsync("lazyverilog.format", "full");
    }

    public Task AutoWireAsync()
    {
        return ExecuteAtCaretAsync("lazyverilog.autowire");
    }

    public Task AutoFfAsync()
    {
        return ExecuteAtCaretAsync("lazyverilog.autoffApply");
    }

    public Task AutoFfAllAsync()
    {
        return ExecuteAsync("lazyverilog.autoffAllApply");
    }

    public Task LintAsync()
    {
        return RunLintAsync(false);
    }

    public Task LintAllAsync()
    {
        return RunLintAsync(true);
    }

    public async Task ShowInterfaceAsync()
    {
        var first = await PromptAsync("Inspect Interface", "First instance name");
        if (first == null) return;
        var second = await PromptAsync("Inspect Interface", "Second instance name (leave empty for a single instance)");
        if (second == null) return;

        var result = string.IsNullOrWhiteSpace(second)
            ? await ExecuteAsync("lazyverilog.singleInterface", first)
            : await ExecuteAsync("lazyverilog.interface", first, second);
        await ShowResultAsync("LazyVerilog Interface", result);
    }

    public async Task ConnectAsync()
    {
        var sourcePath = await PromptAsync("Connect", "Source hierarchical instance path");
        if (sourcePath == null) return;
        var sourcePort = await PromptAsync("Connect", "Source port");
        if (sourcePort == null) return;
        var destinationPath = await PromptAsync("Connect", "Destination hierarchical instance path");
        if (destinationPath == null) return;
        var destinationPort = await PromptAsync("Connect", "Destination port");
        if (destinationPort == null) return;
        var wireName = await PromptAsync("Connect", "Wire name");
        if (wireName == null) return;

        await ExecuteAsync("lazyverilog.connectApply", sourcePath, sourcePort, destinationPath, destinationPort,
            wireName, string.Empty, string.Empty);
    }

    public async Task ConnectInterfacePortsAsync()
    {
        var values = await PromptManyAsync("Connect Interface Ports",
            "First instance", "Second instance", "First port", "Second port", "Wire name", "Wire type");
        if (values == null) return;
        await ExecuteAsync("lazyverilog.interfaceConnect", values);
    }

    public async Task DisconnectInterfacePortsAsync()
    {
        var values = await PromptManyAsync("Disconnect Interface Ports",
            "First instance", "Second instance", "First port", "Second port", "Signal name");
        if (values == null) return;
        await ExecuteAsync("lazyverilog.interfaceDisconnect", values);
    }

    private Task<JToken?> ExecuteAtCaretAsync(string command)
    {
        if (!TryGetContext(out var editor, out _)) return Task.FromResult<JToken?>(null);
        var line = editor.CurrentDocument.GetLocation(editor.Editor.CaretOffset).Line - 1;
        return ExecuteAsync(command, line);
    }

    private async Task<JToken?> ExecuteAsync(string command, params object[] arguments)
    {
        if (!TryGetContext(out var editor, out var service)) return null;

        var commandArguments = new JArray(DocumentUri.FromFileSystemPath(editor.FullPath).ToString());
        foreach (var argument in arguments) commandArguments.Add(JToken.FromObject(argument));

        var result = await service.ExecuteCommandAsync(new Command
        {
            Title = command,
            Name = command,
            Arguments = commandArguments
        });

        if (result is JObject resultObject && resultObject["error"] is JToken error)
            await windowService.ShowMessageAsync("LazyVerilog", error.Value<string>() ?? "Command failed.",
                MessageBoxIcon.Error, mainDockService.GetWindowOwner(editor));

        return result;
    }

    private async Task RunLintAsync(bool all)
    {
        if (!TryGetContext(out var editor, out var service)) return;
        var command = all ? "lazyverilog.lintAll" : "lazyverilog.lint";
        var arguments = all
            ? new JArray()
            : new JArray(DocumentUri.FromFileSystemPath(editor.FullPath).ToString());
        var result = await service.ExecuteCommandAsync(new Command
        {
            Title = command,
            Name = command,
            Arguments = arguments
        });
        if (result is not JArray violations) return;

        if (all) errorService.Clear(VerilogModule.LspName);

        var errors = violations.OfType<JObject>()
            .Select(ToError)
            .Where(error => error != null)
            .Cast<ErrorListItem>()
            .GroupBy(error => error.FilePath);

        foreach (var fileErrors in errors)
            errorService.RefreshErrors(fileErrors.ToList(), VerilogModule.LspName, fileErrors.Key);

        if (!all && violations.Count == 0)
            errorService.RefreshErrors([], VerilogModule.LspName, editor.FullPath);
    }

    private ErrorListItem? ToError(JObject violation)
    {
        var uri = violation.Value<string>("uri");
        var path = !string.IsNullOrWhiteSpace(uri)
            ? DocumentUri.From(uri).GetFileSystemPath()
            : violation.Value<string>("file");
        if (string.IsNullOrWhiteSpace(path)) return null;

        var severity = violation.Value<string>("severity") switch
        {
            "Error" => ErrorType.Error,
            "Warning" => ErrorType.Warning,
            _ => ErrorType.Hint
        };
        var line = Math.Max(violation.Value<int?>("line") ?? 1, 1);
        var column = Math.Max(violation.Value<int?>("col") ?? 1, 1);
        return new ErrorListItem(violation.Value<string>("message") ?? "LazyVerilog diagnostic", severity,
            path, VerilogModule.LspName, line, column, line, column + 1, string.Empty, null,
            projectExplorerService.GetRootFromFile(path));
    }

    private bool TryGetContext(out IEditor editor, out LanguageServiceVerilog service)
    {
        editor = null!;
        service = null!;
        if (mainDockService.CurrentDocument is not IEditor currentEditor ||
            languageManager.GetLanguageService(currentEditor.FullPath) is not LanguageServiceVerilog currentService ||
            !currentService.IsLanguageServiceReady)
            return false;

        editor = currentEditor;
        service = currentService;
        return true;
    }

    private Task<string?> PromptAsync(string title, string message)
    {
        return windowService.ShowInputAsync(title, message, MessageBoxIcon.Info, null,
            mainDockService.CurrentDocument == null
                ? null
                : mainDockService.GetWindowOwner(mainDockService.CurrentDocument));
    }

    private async Task<object[]?> PromptManyAsync(string title, params string[] prompts)
    {
        var values = new object[prompts.Length];
        for (var i = 0; i < prompts.Length; i++)
        {
            var value = await PromptAsync(title, prompts[i]);
            if (value == null) return null;
            values[i] = value;
        }

        return values;
    }

    private Task ShowResultAsync(string title, JToken? result)
    {
        var message = result?.ToString(Formatting.Indented) ?? "No result.";
        return windowService.ShowMessageAsync(title, message, MessageBoxIcon.Info,
            mainDockService.CurrentDocument == null
                ? null
                : mainDockService.GetWindowOwner(mainDockService.CurrentDocument));
    }
}
