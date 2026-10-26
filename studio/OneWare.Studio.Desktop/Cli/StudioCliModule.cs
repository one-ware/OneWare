using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneWare.Essentials.Services;

namespace OneWare.Studio.Desktop.Cli;

internal sealed class StudioCliModule(Func<CancellationToken, Task<int>> stopStudio) : OneWareCliModuleBase
{
    public override IReadOnlyList<Command> RegisterCommands(IServiceProvider serviceProvider)
    {
        var stopStudioCommand = new Command("stop", "Stop OneWare Studio");
        stopStudioCommand.SetAction((_, cancellationToken) => stopStudio(cancellationToken));

        return [stopStudioCommand];
    }
}
