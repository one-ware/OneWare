using System.CommandLine;
using OneWare.Core.Services;

var bootstrapSymbols = OneWareStartupCommandLine.CreateSymbols("oneware:// URI to open");
var bootstrapCommand = OneWareStartupCommandLine.CreateRootCommand(bootstrapSymbols);
bootstrapCommand.TreatUnmatchedTokensAsErrors = false;

var bootstrapParseResult = bootstrapCommand.Parse(args);
OneWareStartupCommandLine.ApplyEnvironmentVariables(bootstrapParseResult, bootstrapSymbols);

var cliSymbols = OneWareStartupCommandLine.CreateSymbols("oneware:// URI to open");
var rootCommand = OneWareStartupCommandLine.CreateRootCommand(cliSymbols);
var studioProcessController = new StudioProcessController();
var cliHostBuilder = CliHostFactory.Create();
var cliModuleLoader = new CliModuleLoader(cliHostBuilder);

cliModuleLoader.RegisterBuiltInCliModules(
    (parseResult, openTarget, detach, cancellationToken) =>
    {
        OneWareStartupCommandLine.ApplyEnvironmentVariables(parseResult, cliSymbols);
        return studioProcessController.StartStudio(openTarget, detach, cancellationToken);
    },
    studioProcessController.StopStudio);

cliModuleLoader.LoadBundledCliModules();
cliModuleLoader.LoadPluginCliModules();

using var cliHost = cliHostBuilder.Build();
foreach (var command in cliHost.ModuleManager.RegisterCommands(cliHost.ServiceProvider))
    rootCommand.Subcommands.Add(command);

if (OneWareStartupCommandLine.ContainsOneWareUriArgument(args))
{
    rootCommand.SetAction(parseResult =>
    {
        if (parseResult.GetValue(cliSymbols.OpenArgument) is null)
            return Task.FromResult(0);

        OneWareStartupCommandLine.ApplyEnvironmentVariables(parseResult, cliSymbols);
        return studioProcessController.StartStudio(
            parseResult.GetValue(cliSymbols.OpenArgument),
            false,
            CancellationToken.None);
    });
}

var parseResult = rootCommand.Parse(args);
return parseResult.Invoke();
