using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Essentials.Services;

public interface IOneWareCliModule
{
    /// <summary>
    /// Unique module ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Module IDs that must be initialized before this one.
    /// </summary>
    IReadOnlyCollection<string> Dependencies { get; }

    /// <summary>
    /// Registers services into the dependency injection container.
    /// </summary>
    void RegisterServices(IServiceCollection services);
    
    /// <summary>
    /// Registers commands into the CLI command tree.
    /// </summary>
    IReadOnlyList<Command> RegisterCommands(IServiceProvider serviceProvider);
}
