using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IFileIconService
{
    /// <summary>
    /// Registers an observable image for the given extensions.
    /// </summary>
    public void RegisterFileIcon(IObservable<IImage> icon, params string[] extensions);
    /// <summary>
    /// Registers a resource icon for the given extensions.
    /// </summary>
    public void RegisterFileIcon(string resourceName, params string[] extensions);
    /// <summary>
    /// Returns an observable image for the given extension.
    /// </summary>
    public IObservable<IImage> GetFileIcon(string extension);
    /// <summary>
    /// Returns an icon model for the given extension.
    /// </summary>
    public IconModel GetFileIconModel(string extension);
}
