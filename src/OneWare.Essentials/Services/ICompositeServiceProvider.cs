using Microsoft.Extensions.DependencyInjection;

namespace OneWare.Essentials.Services;

public interface ICompositeServiceProvider : IServiceProvider, IServiceProviderIsService
{
    
}