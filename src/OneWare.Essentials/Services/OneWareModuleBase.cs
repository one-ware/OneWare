using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Essentials.Services;

public abstract class OneWareModuleBase : IOneWareModule
{
    public virtual string Id => GetType().Name;

    public virtual IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public abstract void RegisterServices(IServiceCollection services);

    public virtual void Initialize(IServiceProvider serviceProvider)
    {
    }
}