using System.Runtime.Serialization;

namespace OneWare.Shared;

[DataContract]
public class ExternalFile : IFile
{
    [DataMember]
    public string FullPath { get; set; }
    public string Header => Path.GetFileName(FullPath);
    public bool LoadingFailed { get; set; }
    public DateTime LastSaveTime { get; set; }

    public ExternalFile(string fullPath)
    {
        FullPath = fullPath;
    }
}