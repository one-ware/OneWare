using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Essentials.Services;

public interface IOneWareModule
{
    string Id { get; }

    IReadOnlyCollection<string> Dependencies { get; }

    void RegisterServices(IServiceCollection services);

    void Initialize(IServiceProvider serviceProvider);
}