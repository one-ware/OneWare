namespace OneWare.Shared;

public interface IProjectEntry : IHasPath
{
    public string RelativePath { get; }

    public IProjectRoot? Root { get; }
    
    public IProjectFolder? TopFolder { get; set; }
    
    public bool IsValid();
}