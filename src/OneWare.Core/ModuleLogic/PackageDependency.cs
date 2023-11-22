namespace OneWare.Core.ModuleLogic;

public class PackageDependency
{
    public string? Name { get; init; }
    
    public string? MinVersion { get; init; }
    
    public string? MaxVersion { get; init; }
}