namespace OneWare.SDK.Models;

public interface IPlugin
{
    public string Id { get; }
    
    public string Path { get; }
    
    public bool IsCompatible { get; }
    
    public string? CompatibilityReport { get; }
}