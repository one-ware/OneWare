using Avalonia.Input;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.LanguageService;

public interface ITypeAssistance
{
    bool CanAddBreakPoints { get; }

    /// <summary>
    /// Regular expression matching the lines that can carry a breakpoint; <see langword="null"/>
    /// means every line qualifies. A language whose lines are not all executable reports the
    /// executable ones here - without it the margin accepts a breakpoint the debugger cannot put
    /// on that line, and the backend silently moves it to the next line that has code.
    /// </summary>
    string? BreakPointLinePattern => null;
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
    Task<List<MenuItemModel>?> GetQuickMenuAsync(int offset);
    Task<string?> GetHoverInfoAsync(int offset);
    Task<Action?> GetActionOnControlWordAsync(int offset);
    IEnumerable<MenuItemModel>? GetTypeAssistanceQuickOptions();
}