using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using OneWare.Chat.ViewModels.ChatMessages;
using OneWare.Essentials.Models;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class ChatMessagePlanReadyViewModelTests
{
    private static (ChatPlanReadyEvent Event, int[] Counts) CreateEvent()
    {
        var counts = new int[2];
        var planEvent = new ChatPlanReadyEvent("Plan is ready.", "1. Do the thing",
            new RelayCommand<Control?>(_ => counts[0]++),
            new RelayCommand<Control?>(_ => counts[1]++));

        return (planEvent, counts);
    }

    [Fact]
    public void StartImplementation_RunsOnceAndRecordsTheDecision()
    {
        var (planEvent, counts) = CreateEvent();
        var message = new ChatMessagePlanReadyViewModel(planEvent);

        message.StartImplementation();
        message.StartImplementation();
        message.UpdatePlan();

        Assert.Equal(1, counts[0]);
        Assert.Equal(0, counts[1]);
        Assert.True(message.IsAnswered);
        Assert.Equal("Implementation started", message.AnswerText);
        Assert.Equal("1. Do the thing", message.PlanMarkdown);
        Assert.Equal("Plan is ready.", message.Summary);
    }

    [Fact]
    public void PlanMarkdown_FallsBackToTheSummaryWhenNoPlanContentIsReported()
    {
        var planEvent = new ChatPlanReadyEvent("## Plan\n\n1. Do the thing", null,
            new RelayCommand<Control?>(_ => { }), new RelayCommand<Control?>(_ => { }));

        var message = new ChatMessagePlanReadyViewModel(planEvent);

        Assert.Equal("## Plan\n\n1. Do the thing", message.PlanMarkdown);
        Assert.Null(message.Summary);
        Assert.False(message.HasSummary);
    }

    [Fact]
    public void Expire_WithdrawsTheChoice()
    {
        var (planEvent, counts) = CreateEvent();
        var message = new ChatMessagePlanReadyViewModel(planEvent);

        planEvent.Expire();
        Dispatcher.UIThread.RunJobs();
        message.StartImplementation();

        Assert.Equal(0, counts[0]);
        Assert.True(message.IsAnswered);
        Assert.Equal("The turn ended before a decision was made.", message.AnswerText);
    }

    [Fact]
    public void Expire_DoesNotOverwriteAnAlreadyMadeDecision()
    {
        var (planEvent, counts) = CreateEvent();
        var message = new ChatMessagePlanReadyViewModel(planEvent);

        message.UpdatePlan();
        planEvent.Expire();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, counts[1]);
        Assert.Equal("Still planning — describe what to change", message.AnswerText);
    }
}
