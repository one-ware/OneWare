using Avalonia.Input;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.LanguageService
{
    public interface ITypeAssistance
    {
        bool CanAddBreakPoints { get; }
        string? LineCommentSequence { get; }
        IFoldingStrategy? FoldingStrategy { get; }
        event EventHandler AssistanceActivated;
        event EventHandler AssistanceDeactivated;
        void Open();
        void Close();
        void Attach();
        void Detach();
        void Comment();
        void Uncomment();
        void AutoIndent();
        void AutoIndent(int startLine, int endLine);
        void Format();
        void TextEntering(TextInputEventArgs e);
        void TextEntered(TextInputEventArgs e);
        void CaretPositionChanged(int offset);
        Task<List<MenuItemViewModel>?> GetQuickMenuAsync(int offset);
        Task<string?> GetHoverInfoAsync(int offset);
        Task<Action?> GetActionOnControlWordAsync(int offset);
        IEnumerable<MenuItemViewModel>? GetTypeAssistanceQuickOptions();
    }
}