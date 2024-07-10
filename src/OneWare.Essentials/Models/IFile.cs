namespace OneWare.Essentials.Models;

public interface IFile : ISavable, ICanHaveIcon
{
    public string Extension { get; }
}