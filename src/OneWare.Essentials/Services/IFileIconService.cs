using Avalonia.Media;

namespace OneWare.Essentials.Services;

public interface IFileIconService
{
    public void RegisterFileIcon(IObservable<IImage> icon, params string[] extensions);
    public void RegisterFileIcon(string resourceName, params string[] extensions);
    public IObservable<IImage> GetFileIcon(string extension);
}