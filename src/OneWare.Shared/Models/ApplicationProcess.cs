using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared.Enums;

namespace OneWare.Shared.Models;

public class ApplicationProcess : ObservableObject
{
    private string? _finishMessage;
    private AppState _state;

    private string? _statusMessage;

    public AppState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string? FinishMessage
    {
        get => _finishMessage;
        set => SetProperty(ref _finishMessage, value);
    }

    public Process? Process { get; init; }

    public bool Terminated { get; set; }

    public Action? Terminate { get; init; }
}