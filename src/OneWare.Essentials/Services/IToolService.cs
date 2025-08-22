using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IToolService
{
    void Register(EnvironmentDescription description);
    
    void Unregister(EnvironmentDescription description);
    void Unregister(string toolKey);
    IReadOnlyList<EnvironmentDescription> GetAllTools(); 
    
    ToolConfiguration GetGlobalToolConfiguration();
}