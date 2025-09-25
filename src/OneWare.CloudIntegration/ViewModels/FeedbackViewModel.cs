using System.Collections.ObjectModel;
using Avalonia.Controls;
using OneWare.CloudIntegration.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.CloudIntegration.ViewModels;

public class FeedbackViewModel : FlexibleWindowViewModelBase
{
    private readonly OneWareCloudLoginService _loginService;
    
    private string _header = string.Empty;
    private string _message = string.Empty;
    private string _category = string.Empty;
    private string _errorText = string.Empty;
    private bool _isLoading = false;
    
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
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }
    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }
    public string ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    public async Task SendFeedbackAsync(Window window)
    {
        if (string.IsNullOrWhiteSpace(Header) ||
            string.IsNullOrWhiteSpace(Message) ||
            string.IsNullOrWhiteSpace(Category))
        {
            ErrorText = "Please fill out the form";
            return;
        }

        IsLoading = true;
        Result = await _loginService.SendFeedbackAsync(Header, Category, Message);
        IsLoading = false;
        window.Close();
    }
    public void Cancel(Window window)
    {
        window.Close();
    }
}