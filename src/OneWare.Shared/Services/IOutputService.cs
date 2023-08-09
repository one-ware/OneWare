using Avalonia.Media;
using Dock.Model.Core;

namespace OneWare.Shared.Services;

public interface IOutputService : IDockable
{
    public void WriteLine(string text, IBrush? textColor = null);
}