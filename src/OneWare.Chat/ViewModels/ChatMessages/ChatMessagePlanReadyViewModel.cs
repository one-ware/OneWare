using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;

namespace OneWare.Chat.ViewModels.ChatMessages;

/// <summary>
/// Shown when the agent finished planning: lets the user start the implementation or keep refining
/// the plan. Stays in the transcript after the choice, so it is visible what was decided.
/// </summary>
public class ChatMessagePlanReadyViewModel : ObservableObject, IChatMessage
{
    public ChatMessagePlanReadyViewModel(ChatPlanReadyEvent planEvent)
    {
        Event = planEvent;

        if (planEvent.IsExpired) Expire();
        // Never block here: the withdrawal can come from the thread that is tearing the turn down.
        else planEvent.Expired += (_, _) =>
        {
            if (Dispatcher.UIThread.CheckAccess()) Expire();
            else Dispatcher.UIThread.Post(Expire);
        };
    }

    public ChatPlanReadyEvent Event { get; }

    /// <summary>
    /// The plan as markdown. Backends do not agree on where the plan text is: some fill
    /// <see cref="ChatPlanReadyEvent.PlanContent"/>, others write the whole plan into the summary,
    /// so whichever carries it is rendered.
    /// </summary>
    public string PlanMarkdown => FirstNonEmpty(Event.PlanContent, Event.Summary) ?? "The plan is ready.";

    /// <summary>Summary line above the plan, shown only when it is not the plan text itself.</summary>
    public string? Summary => string.IsNullOrWhiteSpace(Event.PlanContent) ? null : Trim(Event.Summary);

    public bool HasSummary => Summary != null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(Trim).FirstOrDefault(x => x != null);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Whether the choice was made or has been withdrawn.</summary>
    public bool IsAnswered
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>What happened, shown in place of the buttons once the choice is gone.</summary>
    public string? AnswerText
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public void StartImplementation() => Answer(Event.StartImplementationCommand, "Implementation started");

    public void UpdatePlan() => Answer(Event.UpdatePlanCommand, "Still planning — describe what to change");

    private void Answer(IRelayCommand<Control?> command, string answer)
    {
        if (IsAnswered || Event.IsExpired) return;

        IsAnswered = true;
        AnswerText = answer;
        command.Execute(null);
    }

    private void Expire()
    {
        if (IsAnswered) return;

        IsAnswered = true;
        AnswerText = "The turn ended before a decision was made.";
    }
}
