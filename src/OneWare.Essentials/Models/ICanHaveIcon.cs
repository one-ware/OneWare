using Avalonia.Media;

namespace OneWare.Essentials.Models;

public interface ICanHaveIcon
{
    public IImage? Icon { get; }
}