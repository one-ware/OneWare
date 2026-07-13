using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessageUserInputRequestViewModel : ObservableObject, IChatMessage
{
    public ChatMessageUserInputRequestViewModel(ChatUserInputRequestEvent inputRequestEvent)
    {
        Event = inputRequestEvent;
    }

    public ChatUserInputRequestEvent Event { get; }

    public string FreeformText
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsAnswered
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string? AnswerText
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public void SelectChoice(string? choice) => Submit(choice ?? string.Empty);

    public void SubmitFreeform() => Submit(FreeformText.Trim());

    private void Submit(string answer)
    {
        if (IsAnswered || string.IsNullOrEmpty(answer)) return;

        IsAnswered = true;
        AnswerText = answer;
        Event.SubmitCommand.Execute(answer);
    }
}
