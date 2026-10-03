using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Essentials.Services;

public abstract class OneWareCliModuleBase : IOneWareCliModule
{
    public virtual string Id => GetType().Name;

    public virtual IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public virtual void RegisterServices(IServiceCollection services)
    {
    }

    public virtual IReadOnlyList<Command> RegisterCommands(IServiceProvider serviceProvider)
    {
        return Array.Empty<Command>();
    }
}
