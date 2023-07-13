using Avalonia.Input;
using AvaloniaEdit.CodeCompletion;
using OneWare.Shared.Models;

namespace OneWare.Shared.LanguageService
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
        void Attach(CompletionWindow completion);
        void Detach();
        void Comment();
        void Uncomment();
        void AutoIndent();
        void AutoIndent(int startLine, int endLine);
        void Format();
        Task TextEnteredAsync(TextInputEventArgs e);
        void TextEntering(TextInputEventArgs e);
        Task<List<MenuItemModel>?> GetQuickMenuAsync(int offset);
        Task<string?> GetHoverInfoAsync(int offset);
        Task<Action?> GetActionOnControlWordAsync(int offset);
        IEnumerable<MenuItemModel>? GetTypeAssistanceQuickOptions();
    }
}