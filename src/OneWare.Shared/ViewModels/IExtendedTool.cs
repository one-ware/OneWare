using Avalonia.Media;
using Dock.Model.Core;

namespace OneWare.Shared.ViewModels;

public interface IExtendedTool : IDockable, IWaitForContent
{
    public IImage? Icon { get; }
}