using System.Collections.ObjectModel;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared.Controls;
using OneWare.Shared.Models;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.Views;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCompileViewModel : ObservableObject
{
    private readonly UniversalFpgaProjectRoot _project;
    public string Title => "Connect and Compile - " + _project.Header;

    public ObservableCollection<FpgaModelBase> FpgaModels { get; }

    private FpgaModelBase? _selectedFpga;
    public FpgaModelBase? SelectedFpga
    {
        get => _selectedFpga;
        set => SetProperty(ref _selectedFpga, value);
    }

    private bool _hideExtensions = false;
    public bool HideExtensions
    {
        get => _hideExtensions;
        set => SetProperty(ref _hideExtensions, value);
    }
    
    public UniversalFpgaProjectCompileViewModel(FpgaService fpgaService, NodeProviderService nodeProviderService, UniversalFpgaProjectRoot project)
    {
        _project = project;

        FpgaModels = fpgaService.FpgaModels;
        
        SelectedFpga = FpgaModels.FirstOrDefault();

        if (project.TopEntity is IProjectFile file)
        {
            var provider = nodeProviderService.GetNodeProvider(file.Extension);
            if (provider is not null)
            {
                var nodes = provider.ExtractNodes(file);
                SelectedFpga?.VisibleNodes.AddRange(nodes);
            }
        }
    }

    public async Task SaveAsync(FlexibleWindow window)
    {
        
    }
}