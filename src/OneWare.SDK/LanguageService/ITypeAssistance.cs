using Avalonia.Input;
using AvaloniaEdit.CodeCompletion;
using OneWare.SDK.EditorExtensions;
using OneWare.SDK.Models;
using OneWare.SDK.ViewModels;

namespace OneWare.SDK.LanguageService
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
        Task<List<MenuItemViewModel>?> GetQuickMenuAsync(int offset);
        Task<string?> GetHoverInfoAsync(int offset);
        Task<Action?> GetActionOnControlWordAsync(int offset);
        IEnumerable<MenuItemViewModel>? GetTypeAssistanceQuickOptions();
    }
}