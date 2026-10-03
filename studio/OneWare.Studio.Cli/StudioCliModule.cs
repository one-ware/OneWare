using System.CommandLine;
using OneWare.Essentials.Services;

internal sealed class StudioCliModule(Func<CancellationToken, Task<int>> stopStudio) : OneWareCliModuleBase
{
    public override IReadOnlyList<Command> RegisterCommands(IServiceProvider serviceProvider)
    {
        var stopStudioCommand = new Command("stop", "Stop OneWare Studio");
        stopStudioCommand.SetAction((_, cancellationToken) => stopStudio(cancellationToken));

        return [stopStudioCommand];
    }
}
