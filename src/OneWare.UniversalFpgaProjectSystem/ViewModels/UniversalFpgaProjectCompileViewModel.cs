using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared.Controls;
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
    
    public UniversalFpgaProjectCompileViewModel(FpgaService fpgaService, UniversalFpgaProjectRoot project)
    {
        _project = project;

        FpgaModels = fpgaService.FpgaModels;
        
        SelectedFpga = FpgaModels.FirstOrDefault();
    }

    public async Task SaveAsync(FlexibleWindow window)
    {
        
    }
}