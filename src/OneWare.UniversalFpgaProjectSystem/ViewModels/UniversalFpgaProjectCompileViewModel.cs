using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCompileViewModel : ObservableObject
{
    private readonly UniversalFpgaProjectRoot _project;
    public string Title => "Connect and Compile - " + _project.Header;

    public ObservableCollection<FpgaModel> FpgaModels { get; } = new();

    public ObservableCollection<string> Signals { get; } = new();

    public ObservableCollection<string> Pins { get; } = new();
    
    public UniversalFpgaProjectCompileViewModel(UniversalFpgaProjectRoot project)
    {
        _project = project;
    }
}