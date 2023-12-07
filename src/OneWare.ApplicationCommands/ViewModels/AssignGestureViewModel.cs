using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.SDK.Controls;
using OneWare.SDK.Models;
using OneWare.SDK.ViewModels;

namespace OneWare.ApplicationCommands.ViewModels;

public partial class AssignGestureViewModel(IApplicationCommand command) : FlexibleWindowViewModelBase
{
    public IApplicationCommand ApplicationCommand { get; } = command;
    
    private KeyGesture? _capturedKeyGesture = command.ActiveGesture;
    public KeyGesture? CapturedKeyGesture
    {
        get => _capturedKeyGesture;
        set => SetProperty(ref _capturedKeyGesture, value);
    }

    public void Clear()
    {
        CapturedKeyGesture = null;
    }
    
    public void Reset()
    {
        CapturedKeyGesture = ApplicationCommand.DefaultGesture;
    }
    
    public void Cancel(FlexibleWindow window)
    {
        window.Close();
    }
    
    public void Save(FlexibleWindow window)
    {
        ApplicationCommand.ActiveGesture = CapturedKeyGesture;
        window.Close();
    }
}