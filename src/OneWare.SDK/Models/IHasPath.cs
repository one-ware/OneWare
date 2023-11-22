namespace OneWare.SDK.Models;

public interface IHasPath
{
    public string FullPath { get; }
    public string Header { get; }
    public bool LoadingFailed { get; set; }
}