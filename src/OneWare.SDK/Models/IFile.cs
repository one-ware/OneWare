using Avalonia.Media;

namespace OneWare.SDK.Models;

public interface IFile : ISavable
{
    public string Extension { get; }
    public IImage? Icon { get; }
}