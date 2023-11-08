using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared.Controls;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCompileViewModel : ObservableObject
{
    private readonly UniversalFpgaProjectRoot _project;
    public string Title => "Connect and Compile - " + _project.Header;

    public ObservableCollection<FpgaModel> FpgaModels { get; } = new();

    private FpgaModel? _selectedFpgaModel;
    public FpgaModel? SelectedFpgaModel
    {
        get => _selectedFpgaModel;
        set => SetProperty(ref _selectedFpgaModel, value);
    }

    public ObservableCollection<string> Signals { get; } = new();

    public ObservableCollection<string> Pins { get; } = new();

    private bool _hideExtensions = false;
    public bool HideExtensions
    {
        get => _hideExtensions;
        set => SetProperty(ref _hideExtensions, value);
    }
    
    public UniversalFpgaProjectCompileViewModel(UniversalFpgaProjectRoot project)
    {
        _project = project;
        
        FpgaModels.Add(new FpgaModel("Max 10"));
        SelectedFpgaModel = FpgaModels.First();
    }

    public async Task SaveAsync(FlexibleWindow window)
    {
        
    }
}