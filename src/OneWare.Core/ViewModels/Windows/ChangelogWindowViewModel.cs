using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using OneWare.Shared.ViewModels;

namespace OneWare.Core.ViewModels.Windows;

public class ChangelogWindowViewModel : ViewModelBase
{
    private bool _isLoading = false;
    
    private readonly string _changelogUrl = "https://raw.githubusercontent.com/VHDPlus/vhdplus-website/master/docs/ide/changelog.md";

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

    public ChangelogWindowViewModel()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        
        using var client = new HttpClient();
        using (var response = await client.GetAsync(_changelogUrl))
        {
            using (HttpContent content = response.Content)
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