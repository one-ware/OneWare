using System.Collections.ObjectModel;
using Avalonia.Threading;
using OneWare.Debugger.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

public class DebuggerCallStackViewModel : ExtendedTool
{
    public const string IconKey = "VsCodeLight.Debug";

    private readonly IDebuggerService _debuggerService;
    private readonly IMainDockService _mainDockService;
    private bool _isRunning;
    private CallStackFrameViewModel? _selectedFrame;

    public DebuggerCallStackViewModel(IDebuggerService debuggerService, IMainDockService mainDockService) : base(IconKey)
    {
        _debuggerService = debuggerService;
        _mainDockService = mainDockService;
        Id = "DebugCallStack";
        _debuggerService.StateChanged += (_, _) => Dispatcher.UIThread.Post(RefreshState);
        RefreshState();
    }

    public ObservableCollection<CallStackFrameViewModel> Frames { get; } = new();

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public CallStackFrameViewModel? SelectedFrame
    {
        get => _selectedFrame;
        set
        {
            if (!SetProperty(ref _selectedFrame, value) || value == null)
                return;

            _ = OpenFrameAsync(value);
        }
    }

    public override void InitializeContent()
    {
        base.InitializeContent();
        Title = "Debug Call Stack";
    }

    private async Task OpenFrameAsync(CallStackFrameViewModel frame)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(frame.FullPath) && File.Exists(frame.FullPath))
            {
                if (await _mainDockService.OpenFileAsync(frame.FullPath) is IEditor editor && frame.Line > 0)
                    editor.JumpToLine(frame.Line);
            }
        }
        finally
        {
            SelectedFrame = null;
        }
    }

    private void RefreshState()
    {
        var state = _debuggerService.CurrentState;
        IsRunning = state.IsRunning;
        Frames.Clear();

        if (IsRunning)
            return;

        foreach (var frame in state.CallStack)
            Frames.Add(CallStackFrameViewModel.FromModel(frame));
    }
}
