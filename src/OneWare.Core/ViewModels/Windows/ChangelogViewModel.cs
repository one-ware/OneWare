using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.ViewModels.Windows;

public class ChangelogViewModel : FlexibleWindowViewModelBase
{
    private readonly string _changelogUrl;
    private readonly IHttpService _httpService;

    private string? _changeLog;

    private bool _isLoading;

    public ChangelogViewModel(IPaths paths, IHttpService httpService)
    {
        _httpService = httpService;
        Title = "Changelog";
        Id = "Changelog";
        _changelogUrl = paths.ChangelogUrl;
        _ = LoadAsync();
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string? ChangeLog
    {
        get => _changeLog;
        set => SetProperty(ref _changeLog, value);
    }

    private async Task LoadAsync()
    {
        IsLoading = true;

        var text = await _httpService.DownloadTextAsync(_changelogUrl);

        if (text != null)
        {
            var split = text.Split("---");

            if (split.Length > 0) ChangeLog = split.Last();
        }

        IsLoading = false;
    }
}