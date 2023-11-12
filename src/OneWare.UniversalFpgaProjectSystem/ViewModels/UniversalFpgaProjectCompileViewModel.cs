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
        CreatePcf();
    }
    
    private string RemoveLine(string file, string find)
    {
        var startIndex = file.IndexOf(find, StringComparison.Ordinal);
        while (startIndex > -1)
        {
            var endIndex = file.IndexOf('\n', startIndex);
            if (endIndex == -1) endIndex = file.Length - 1;
            file = file.Remove(startIndex, endIndex - startIndex + 1);
            startIndex = file.IndexOf(find, startIndex, StringComparison.Ordinal);
        }

        return file;
    }
    
    private void CreatePcf()
    {
        if (SelectedFpga == null) return;
        var pcfPath = Path.Combine(_project.FullPath, "project.pcf");
            
        var pcf = "";
        if (File.Exists(pcfPath))
        {
            var existingPcf = File.ReadAllText(pcfPath);
            existingPcf = RemoveLine(existingPcf, "set_io");
            pcf = existingPcf.Trim() + "\n";
        }

        foreach (var conn in SelectedFpga.Pins.Where(x => x.Value.Connection is not null))
        {
            pcf += $"set_io {conn.Value.Connection!.Name} {conn.Value.Name}\n";
        }
            
        File.WriteAllText(pcfPath, pcf);
    }
}