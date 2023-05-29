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

        void Initialize(CompletionWindow completion);

        void Close();

        void Format();

        /// <summary>
        ///     Comment selection or current caret
        /// </summary>
        void Comment();

        /// <summary>
        ///     Uncomment selection or current caret
        /// </summary>
        void Uncomment();

        void Format(int startLine, int endLine);

        Task TextEnteredAsync(TextInputEventArgs e);

        void TextEntering(TextInputEventArgs e);

        /// <summary>
        ///     Request Quick Menu
        /// </summary>
        Task<List<MenuItemViewModel>?> GetQuickMenuAsync(int offset);

        Task<string?> GetHoverInfoAsync(int offset);

        Task<Action?> GetActionOnControlWordAsync(int offset);

        /// <summary>
        ///     Request Quick Options
        /// </summary>
        List<MenuItemViewModel>? GetTypeAssistanceQuickOptions();
    }
}