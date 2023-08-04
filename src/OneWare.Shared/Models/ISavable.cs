namespace OneWare.Shared.Models;

public interface ISavable : IHasPath
{
    public DateTime LastSaveTime { get; set; }
}