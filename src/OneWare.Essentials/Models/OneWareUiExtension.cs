using Avalonia.Controls;

namespace OneWare.Essentials.Models;

public class OneWareUiExtension(Func<object?, Control?> createUiExtension)
{
    public readonly Func<object?, Control?> CreateUiExtension = createUiExtension;
}