using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectToolBarViewModel : ObservableObject
{
    private bool _longTermProgramming;

    public bool LongTermProgramming
    {
        get => _longTermProgramming;
        set => SetProperty(ref _longTermProgramming, value);
    }
    
    public async Task CompileAsync()
    {
        await Task.Delay(100);
    }

    public async Task DownloadAsync()
    {
        await Task.Delay(100);
    }
}