using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.Output;

public class LineContext
{
    public IBrush? LineColor { get; init; }
    
    public IProjectRoot? Owner { get; init; }
}