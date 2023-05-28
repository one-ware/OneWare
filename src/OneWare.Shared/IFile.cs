namespace OneWare.Shared;

public interface IFile : IHasPath
{
    public string Extension => Path.GetExtension(FullPath);
    public DateTime LastSaveTime { get; set; }
}