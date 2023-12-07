using Avalonia.Media;

namespace OneWare.SDK.Models;

public interface IFile : ISavable, ICanHaveIcon
{
    public string Extension { get; }
}