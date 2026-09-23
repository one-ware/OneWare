using OneWare.Essentials.LanguageService;
using OneWare.Essentials.ViewModels;

namespace OneWare.Python;

internal class TypeAssistancePython : TypeAssistanceLanguageService
{
    public TypeAssistancePython(IEditor editor, LanguageServicePython ls) : base(editor, ls)
    {
        CodeBox.TextArea.IndentationStrategy =
            IndentationStrategy = new PythonIndentationStrategy(CodeBox.Options);
        LineCommentSequence = "#";
    }

    public override bool CanAddBreakPoints => false;
}
