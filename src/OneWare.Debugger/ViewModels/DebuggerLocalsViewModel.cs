using System.Collections.ObjectModel;
using Avalonia.Threading;
using OneWare.Debugger.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

public class DebuggerLocalsViewModel : ExtendedTool
{
    public const string IconKey = "VsCodeLight.Debug";

    private readonly IDebuggerService _debuggerService;
    private bool _isRunning;

    public DebuggerLocalsViewModel(IDebuggerService debuggerService) : base(IconKey)
    {
        _debuggerService = debuggerService;
        Id = "DebugLocals";
        _debuggerService.StateChanged += (_, _) => Dispatcher.UIThread.Post(RefreshState);
        RefreshState();
    }

    public ObservableCollection<DebugVariableViewModel> Children { get; } = new();

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public override void InitializeContent()
    {
        base.InitializeContent();
        Title = "Debug Locals";
    }

    private void RefreshState()
    {
        var state = _debuggerService.CurrentState;
        IsRunning = state.IsRunning;
        Children.Clear();

        if (IsRunning)
            return;

        foreach (var variable in state.Locals)
            Children.Add(DebugVariableViewModel.FromModel(variable));
    }
}
