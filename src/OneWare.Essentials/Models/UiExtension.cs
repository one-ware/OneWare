using Avalonia.Controls;

namespace OneWare.Essentials.Models;

public class UiExtension(Func<object?, Control?> createUiExtension)
{
    public readonly Func<object?, Control?> CreateUiExtension = createUiExtension;
}