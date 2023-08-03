namespace OneWare.Shared;

public interface ISavable : IHasPath
{
    public DateTime LastSaveTime { get; set; }
}