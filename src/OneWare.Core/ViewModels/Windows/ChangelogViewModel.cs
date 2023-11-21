using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.Core.ViewModels.Windows;

public class ChangelogViewModel : FlexibleWindowViewModelBase
{
    private bool _isLoading = false;
    
    private string _changelogUrl;

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

    public ChangelogViewModel(IPaths paths)
    {
        Title = "Changelog";
        Id = "Changelog";
        _changelogUrl = paths.ChangelogUrl;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        
        using var client = new HttpClient();
        using (var response = await client.GetAsync(_changelogUrl))
        {
            using (var content = response.Content)
            {
                var result = await content.ReadAsStringAsync();

                var split = result.Split("---");

                if (split.Length > 0)
                {
                    ChangeLog = split.Last();
                }
            }
        }
        IsLoading = false;
    }
}