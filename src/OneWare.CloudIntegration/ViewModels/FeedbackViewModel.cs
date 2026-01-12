using System.Collections.ObjectModel;
using Avalonia.Controls;
using OneWare.CloudIntegration.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.CloudIntegration.ViewModels;

public class FeedbackViewModel : FlexibleWindowViewModelBase
{
    private readonly OneWareCloudLoginService _loginService;
    
    private string _header = string.Empty;

    public FeedbackViewModel(OneWareCloudLoginService loginService)
    {
        _loginService = loginService;
        
        Title = $"Send Feedback";
        Category = "General Feedback";
    }
    
    public bool? Result { get; private set; }
    public ObservableCollection<string> Categories { get; } = [
        "General Feedback",
        "Bug Report",
        "Feature Request",
        "Account/Billing",
        "Performance Issue",
        "Documentation",
        "Other"
    ];

    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    } = false;

    public string Message
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;
    
    public string Mail
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string Category
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string ErrorText
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public async Task SendFeedbackAsync(Window window)
    {
        if (string.IsNullOrWhiteSpace(Message) ||
            string.IsNullOrWhiteSpace(Category))
        {
            ErrorText = "Please fill out the form";
            return;
        }

        IsLoading = true;
        Result = await _loginService.SendFeedbackAsync(Category, Message, Mail);
        IsLoading = false;
        window.Close();
    }
    public void Cancel(Window window)
    {
        window.Close();
    }
}