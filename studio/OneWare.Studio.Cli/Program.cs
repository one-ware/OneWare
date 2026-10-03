using System.CommandLine;
using OneWare.Core.Services;

var bootstrapSymbols = OneWareStartupCommandLine.CreateSymbols("oneware:// URI to open");
var bootstrapCommand = OneWareStartupCommandLine.CreateRootCommand(bootstrapSymbols);
bootstrapCommand.TreatUnmatchedTokensAsErrors = false;

var bootstrapParseResult = bootstrapCommand.Parse(args);
OneWareStartupCommandLine.ApplyEnvironmentVariables(bootstrapParseResult, bootstrapSymbols);

var cliSymbols = OneWareStartupCommandLine.CreateSymbols(
    "File, folder or oneware:// URI to open. Starts OneWare Studio without opening a target when omitted.");
var rootCommand = OneWareStartupCommandLine.CreateRootCommand(cliSymbols);
var detachOption = new Option<bool>("--detach", "-d")
{
    Description = "Start OneWare Studio detached from the current terminal."
};
rootCommand.Options.Add(detachOption);

var studioProcessController = new StudioProcessController();
var cliHostBuilder = CliHostFactory.Create();
var cliModuleLoader = new CliModuleLoader(cliHostBuilder);

cliModuleLoader.RegisterBuiltInCliModules(studioProcessController.StopStudio);

cliModuleLoader.LoadBundledCliModules();
if (!ExtensionStoreCliModule.IsExtensionStoreCommand(args))
    cliModuleLoader.LoadPluginCliModules();

using var cliHost = cliHostBuilder.Build();
foreach (var command in cliHost.ModuleManager.RegisterCommands(cliHost.ServiceProvider))
    rootCommand.Subcommands.Add(command);

rootCommand.SetAction((parseResult, cancellationToken) =>
{
    OneWareStartupCommandLine.ApplyEnvironmentVariables(parseResult, cliSymbols);
    return studioProcessController.StartStudio(
        parseResult.GetValue(cliSymbols.OpenArgument),
        parseResult.GetValue(detachOption),
        cancellationToken);
});

var parseResult = rootCommand.Parse(args);
return parseResult.Invoke();
