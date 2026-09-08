using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Verilog;

public class LanguageServiceVerilog : LanguageServiceLspAutoDownload
{
    private readonly ISettingsService _settingsService;

    public LanguageServiceVerilog(string workspace, ISettingsService settingsService, IPackageService packageService)
        : base(settingsService.GetSettingObservable<string>(VerilogModule.LspPathSetting),
            VerilogModule.LazyVerilogPackage,
            VerilogModule.LspName, workspace, packageService,
            settingsService.GetSettingObservable<bool>("Experimental_AutoDownloadBinaries"))
    {
        _settingsService = settingsService;
    }

    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistanceVerilog(editor, this, _settingsService);
    }

    protected override string GetLanguageId(string fullPath)
    {
        return VerilogModule.SystemVerilogExtensions.Contains(Path.GetExtension(fullPath),
            StringComparer.OrdinalIgnoreCase)
            ? "systemverilog"
            : "verilog";
    }

    public override async Task<JToken?> ExecuteCommandAsync(Command cmd)
    {
        var commandName = cmd.Name switch
        {
            "lazyverilog.autoffPreview" => "lazyverilog.autoffApply",
            "lazyverilog.autoffAllPreview" => "lazyverilog.autoffAllApply",
            _ => cmd.Name
        };

        var result = await base.ExecuteCommandAsync(new Command
        {
            Title = cmd.Title,
            Name = commandName,
            Arguments = cmd.Arguments
        });

        if (result is JObject resultObject && resultObject["changes"] != null)
        {
            try
            {
                var edit = result.ToObject<WorkspaceEdit>(LspSerializer.Instance.JsonSerializer);
                await ApplyWorkspaceEditAsync(edit);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }

        return result;
    }
}
