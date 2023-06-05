namespace OneWare.Shared;

public interface IFile : IHasPath
{
    public string Extension { get; }
    public DateTime LastSaveTime { get; set; }
}