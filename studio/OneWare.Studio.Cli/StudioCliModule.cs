using System.CommandLine;
using OneWare.Essentials.Services;

internal sealed class StudioCliModule(
    Func<ParseResult, string?, bool, CancellationToken, Task<int>> startStudio,
    Func<CancellationToken, Task<int>> stopStudio) : OneWareCliModuleBase
{
    public override IReadOnlyList<Command> RegisterCommands(IServiceProvider serviceProvider)
    {
        var studioCommand = new Command("studio", "OneWare Studio commands");
        studioCommand.Aliases.Add("desktop");

        var startStudioCommand = new Command("start", "Start OneWare Studio and optionally open a file or folder");
        var detachOption = new Option<bool>("--detach", "-d")
        {
            Description = "Start OneWare Studio detached from the current terminal."
        };
        var openTargetArgument = new Argument<string?>("path")
        {
            Description = "File or folder to open. Starts OneWare Studio without opening a target when omitted.",
            DefaultValueFactory = _ => null
        };
        startStudioCommand.Options.Add(detachOption);
        startStudioCommand.Arguments.Add(openTargetArgument);
        startStudioCommand.SetAction((parseResult, cancellationToken) =>
            startStudio(
                parseResult,
                parseResult.GetValue(openTargetArgument),
                parseResult.GetValue(detachOption),
                cancellationToken));
        studioCommand.Subcommands.Add(startStudioCommand);

        var stopStudioCommand = new Command("stop", "Stop OneWare Studio");
        stopStudioCommand.SetAction((_, cancellationToken) => stopStudio(cancellationToken));
        studioCommand.Subcommands.Add(stopStudioCommand);

        return [studioCommand];
    }
}
