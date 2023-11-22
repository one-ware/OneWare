using OneWare.SDK.ViewModels;

namespace OneWare.SDK.LanguageService
{
    internal class GenericTypeAssistanceLsp : TypeAssistanceLsp
    {
        public GenericTypeAssistanceLsp(IEditor editor, GenericLanguageService languageService) : base(editor, languageService)
        {
            CodeBox.TextArea.IndentationStrategy = new LspIndentationStrategy(CodeBox.Options, languageService, editor.CurrentFile);

            // EditorThemeManager.Instance.Languages[highlightingName].WhenValueChanged(x => x.SelectedTheme).Subscribe(
            //     theme =>
            //     {
            //         if (theme == null) return;
            //         CodeBox.SyntaxHighlighting = theme.Load();
            //         CustomHighlightManager = new CustomHighlightManager(CodeBox.SyntaxHighlighting);
            //     });

            FoldingStrategy = new LspFoldingStrategy(languageService, Editor.CurrentFile);
        }
    }
}