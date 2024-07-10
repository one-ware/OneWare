using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using OneWare.Essentials.ViewModels;

namespace OneWare.ChatBot.ViewModels;

public class ChatBotViewModel : ExtendedTool
{
    public const string IconKey = "MaterialDesign.ChatBubbleOutline";
    
    private string _currentMessage = string.Empty;
    public string CurrentMessage
    {
        get => _currentMessage;
        set => this.SetProperty(ref _currentMessage, value);
    }
    
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    private readonly IImage _happyIcon;

    private readonly IImage _userIcon;
     
    public ChatBotViewModel() : base(IconKey)
    {
        Id = "ChatBot";
        Title = "OneAI Chat";
        
        using var stream = AssetLoader.Open(new Uri($"avares://OneWare.ChatBot/Assets/OneAi_Happy.png"));
        _happyIcon = new Bitmap(stream);
        
        _userIcon = Application.Current!.FindResource(ThemeVariant.Light,  "Cool.User") as IImage ?? throw new Exception();
    }

    public void SendText()
    {
        if(string.IsNullOrWhiteSpace(CurrentMessage)) return;
        
        Messages.Add(new ChatMessageViewModel("You", _userIcon)
        {
            Message = CurrentMessage
        });
        
        CurrentMessage = string.Empty;
    }
}