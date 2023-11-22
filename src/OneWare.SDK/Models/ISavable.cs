namespace OneWare.SDK.Models;

public interface ISavable : IHasPath
{
    public DateTime LastSaveTime { get; set; }
}