using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.Shared.Views;
using OneWare.WaveFormViewer.ViewModels;

namespace OneWare.VcdViewer.ViewModels;

public class VcdViewModel : ExtendedDocument
{
    private readonly IProjectExplorerService _projectExplorerService;
    public WaveFormViewModel WaveFormViewer { get; } = new();

    public VcdViewModel(string fullPath, IProjectExplorerService projectExplorerService) : base(fullPath, projectExplorerService)
    {
        _projectExplorerService = projectExplorerService;
        
        Title = $"Loading {Path.GetFileName(fullPath)}";
    }

    protected override void ChangeCurrentFile(IFile? oldFile)
    {
        _ = LoadAsync();
    }

    private Task<bool> LoadAsync()
    {
        try
        {
            File.ReadAllText(FullPath);
        }
        catch (Exception e)
        {
            
        }

        LoadingFailed = false;
        IsLoading = false;
        return Task.FromResult(true);
    }
}