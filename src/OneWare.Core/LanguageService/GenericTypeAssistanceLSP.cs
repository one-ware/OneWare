using System;
using AvaloniaEdit;
using DynamicData.Binding;
using OneWare.Core.EditorExtensions;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Shared;

namespace OneWare.Core.LanguageService
{
    internal class GenericTypeAssistanceLsp : TypeAssistanceLsp, ITypeAssistance
    {
        public GenericTypeAssistanceLsp(TextEditor editor, IFile file, EditViewModel evm,
            GenericLanguageService languageService) : base(file, evm,
            languageService)
        {
            CodeBox.TextArea.IndentationStrategy = new LspIndentationStrategy(CodeBox.Options, languageService, file);

            // EditorThemeManager.Instance.Languages[highlightingName].WhenValueChanged(x => x.SelectedTheme).Subscribe(
            //     theme =>
            //     {
            //         if (theme == null) return;
            //         CodeBox.SyntaxHighlighting = theme.Load();
            //         CustomHighlightManager = new CustomHighlightManager(CodeBox.SyntaxHighlighting);
            //     });

            FoldingStrategy = new LspFoldingStrategy(languageService, file);
        }
    }
}