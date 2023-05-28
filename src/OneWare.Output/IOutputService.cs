using Avalonia.Media;
using Dock.Model.Core;

namespace OneWare.Output;

public interface IOutputService : IDockable
{
    public void WriteLine(string text, IBrush? textColor = null);
}