using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.ViewModels.Windows;

public class ChangelogViewModel : FlexibleWindowViewModelBase
{
    private readonly IHttpService _httpService;
    
    private bool _isLoading;
    
    private readonly string _changelogUrl;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }
    
    private string? _changeLog;
    public string? ChangeLog
    {
        get => _changeLog;
        set => SetProperty(ref _changeLog, value);
    }

    public ChangelogViewModel(IPaths paths, IHttpService httpService)
    {
        _httpService = httpService;
        Title = "Changelog";
        Id = "Changelog";
        _changelogUrl = paths.ChangelogUrl;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;

        var text = await _httpService.DownloadTextAsync(_changelogUrl);

        if (text != null)
        {
            var split = text.Split("---");

            if (split.Length > 0)
            {
                ChangeLog = split.Last();
            }
        }
        
        IsLoading = false;
    }
}