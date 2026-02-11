using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Essentials.Services;

public interface IOneWareModule
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
    /// Initializes the module after services are registered.
    /// </summary>
    void Initialize(IServiceProvider serviceProvider);
}
