using Avalonia.Media;
using Dock.Model.Core;

namespace OneWare.Essentials.ViewModels;

public interface IExtendedTool : IDockable, IWaitForContent
{
    public IImage? Icon { get; }
}