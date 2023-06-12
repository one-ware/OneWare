using Avalonia.Input;
using AvaloniaEdit.CodeCompletion;
using OneWare.Shared.Models;

namespace OneWare.Shared.LanguageService
{
    public interface ITypeAssistance
    {
        bool CanAddBreakPoints { get; }
        string LineCommentSequence { get; }
        LanguageServiceBase Service { get; }
        IFoldingStrategy? FoldingStrategy { get; }
        event EventHandler AssistanceActivated;
        event EventHandler AssistanceDeactivated;
        void Open();
        void Close();
        void Attach(CompletionWindow completion);
        void Detach();
        void Format();
        void Comment();
        void Uncomment();

        void Format(int startLine, int endLine);

        Task TextEnteredAsync(TextInputEventArgs e);

        void TextEntering(TextInputEventArgs e);

        /// <summary>
        ///     Request Quick Menu
        /// </summary>
        Task<List<MenuItemModel>?> GetQuickMenuAsync(int offset);

        Task<string?> GetHoverInfoAsync(int offset);

        Task<Action?> GetActionOnControlWordAsync(int offset);

        /// <summary>
        ///     Request Quick Options
        /// </summary>
        List<MenuItemModel>? GetTypeAssistanceQuickOptions();
    }
}