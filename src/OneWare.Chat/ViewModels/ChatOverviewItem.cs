using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Chat.ViewModels;

/// <summary>A row on the chat overview: a stored chat, and the loaded chat while it is open or working.</summary>
public class ChatOverviewItem(string key) : ObservableObject
{
    internal string Key { get; } = key;

    internal static string BuildKey(string serviceName, string sessionId) => $"{serviceName}\n{sessionId}";

    /// <summary>The loaded chat, for its working and attention state; null for chats only in the history.</summary>
    public ChatSessionViewModel? Session
    {
        get;
        set => SetProperty(ref field, value);
    }

    internal ChatViewModel.ChatSessionHistoryItem? HistoryItem { get; set; }

    public string Title
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string ServiceName
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public DateTimeOffset UpdatedAt
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) OnPropertyChanged(nameof(UpdatedAtLabel));
        }
    }

    public string UpdatedAtLabel
    {
        get
        {
            var local = UpdatedAt.ToLocalTime();
            return local.Date == DateTimeOffset.Now.Date
                ? local.ToString("HH:mm")
                : local.ToString("yyyy-MM-dd");
        }
    }
}
