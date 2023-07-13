using Avalonia.Media;
using Dock.Model.Core;

namespace OneWare.Shared;

public interface IExtendedTool : IDockable
{
    public IImage? Icon { get; }
}