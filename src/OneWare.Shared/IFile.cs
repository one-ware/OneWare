using Avalonia.Media;

namespace OneWare.Shared;

public interface IFile : ISavable
{
    public string Extension { get; }
    public IImage? Icon { get; }
}