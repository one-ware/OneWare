using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IToolService
{
    void Register(ToolDescription description);
    
    void Unregister(ToolDescription description);
    void Unregister(string toolKey);
}