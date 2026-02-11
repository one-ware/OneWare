using Avalonia.Media;
using Dock.Model.Core;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IOutputService : IDockable
{
    /// <summary>
    /// Writes a line to the output panel.
    /// </summary>
    public void WriteLine(string text, IBrush? textColor = null, IProjectRoot? owner = null);

    /// <summary>
    /// Writes text to the output panel without a newline.
    /// </summary>
    public void Write(string text, IBrush? textColor = null, IProjectRoot? owner = null);
}
