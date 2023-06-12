using Avalonia.Media;

namespace OneWare.Shared;

public interface IFile : IHasPath
{
    public string Extension { get; }
    public IImage? Icon { get; }
    public DateTime LastSaveTime { get; set; }
}