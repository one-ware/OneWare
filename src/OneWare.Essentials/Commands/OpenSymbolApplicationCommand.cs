using Avalonia.LogicalTree;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Commands;

public class OpenSymbolApplicationCommand(string name, string file, int line, int character)
    : ApplicationCommandBase(name)
{
    public override bool Execute(ILogical source)
    {
        _ = OpenAsync();
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        return File.Exists(file);
    }

    private async Task OpenAsync()
    {
        if (await ContainerLocator.Container.Resolve<IMainDockService>().OpenFileAsync(file) is not IEditor editor)
            return;

        var documentLine = editor.CurrentDocument.GetLineByNumber(Math.Min(line + 1, editor.CurrentDocument.LineCount));
        editor.Select(Math.Min(documentLine.Offset + character, documentLine.EndOffset), 0);
    }
}
