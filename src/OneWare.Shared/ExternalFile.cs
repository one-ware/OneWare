namespace OneWare.Shared;

public class ExternalFile : IFile
{
    public string FullPath { get; set; }
    public string Header => Path.GetFileName(FullPath);
    public bool LoadingFailed { get; set; }
    public DateTime LastSaveTime { get; set; }

    public ExternalFile(string fullPath)
    {
        FullPath = fullPath;
    }
}