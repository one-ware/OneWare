using Avalonia.Media;

namespace OneWare.Shared.Models;

public interface IFile : ISavable
{
    public string Extension { get; }
    public IImage? Icon { get; }
}