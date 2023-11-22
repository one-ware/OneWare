using Avalonia.Media;
using Dock.Model.Core;

namespace OneWare.SDK.Services;

public interface IOutputService : IDockable
{
    public void WriteLine(string text, IBrush? textColor = null);
}