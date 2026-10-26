using Avalonia.Input;
using OneWare.Chat.Views;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class ChatViewPasteShortcutTests
{
    [Theory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Meta)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Meta)]
    public void IsPasteModifier_AcceptsControlAndCommand(KeyModifiers modifiers)
    {
        Assert.True(ChatView.IsPasteModifier(modifiers));
    }

    [Theory]
    [InlineData(KeyModifiers.None)]
    [InlineData(KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Meta | KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Meta | KeyModifiers.Alt)]
    public void IsPasteModifier_RejectsOtherCombinations(KeyModifiers modifiers)
    {
        Assert.False(ChatView.IsPasteModifier(modifiers));
    }
}
