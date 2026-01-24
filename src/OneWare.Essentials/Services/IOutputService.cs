using Avalonia.Media;
using Dock.Model.Core;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IOutputService : IDockable
{
    public void WriteLine(string text, IBrush? textColor = null, IProjectRoot? owner = null);

    public void Write(string text, IBrush? textColor = null, IProjectRoot? owner = null);
}