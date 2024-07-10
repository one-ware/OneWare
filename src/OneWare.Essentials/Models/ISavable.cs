namespace OneWare.Essentials.Models;

public interface ISavable : IHasPath
{
    public DateTime LastSaveTime { get; set; }
}