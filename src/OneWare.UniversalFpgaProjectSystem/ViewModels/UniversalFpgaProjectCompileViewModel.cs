using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData.Binding;
using OneWare.Shared.Controls;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCompileViewModel : FlexibleWindowViewModelBase
{
    private readonly IWindowService _windowService;
    private readonly UniversalFpgaProjectRoot _project;

    public ObservableCollection<FpgaModelBase> FpgaModels { get; } = new();

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
    
    public UniversalFpgaProjectCompileViewModel(IWindowService windowService, FpgaService fpgaService, NodeProviderService nodeProviderService, UniversalFpgaProjectRoot project)
    {
        _windowService = windowService;
        _project = project;

        this.WhenValueChanged(x => x.IsDirty).Subscribe(x =>
        {
            Title = $"Connect and Compile - {_project.Header}{(x ? "*" : "")}";
        });
        
        foreach (var fpga in fpgaService.GetFpgas())
        {
            Observable.FromEventPattern(fpga, nameof(fpga.NodeConnected)).Subscribe(_ =>
            {
                IsDirty = true;
            });

            Observable.FromEventPattern(fpga, nameof(fpga.NodeDisconnected)).Subscribe(_ =>
            {
                IsDirty = true;
            });
            
            FpgaModels.Add(fpga);
        }
        
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

    public override void Close(FlexibleWindow window)
    {
        if(!IsDirty) window.Close();
        _ = SafeQuitAsync(window);
    }
    
    private async Task SafeQuitAsync(FlexibleWindow window)
    {
        var result = await _windowService.ShowYesNoCancelAsync("Warning", "Do you want to save changes?", MessageBoxIcon.Warning, window.Host);
        
        switch (result)
        {
            case MessageBoxStatus.Yes:
                SaveAndClose(window);
                break;
            case MessageBoxStatus.No:
                IsDirty = false;
                break;
            case MessageBoxStatus.Canceled:
                return;
        }
        
        IsDirty = false;
        window.Close();
    }
    
    public void SaveAndClose(FlexibleWindow window)
    {
        CreatePcf();
        IsDirty = false;
        window.Close();
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
    
    private void LoadConnectionsFromPcf(string pcf, FpgaModelBase fpga)
    {
        var lines = pcf.Split('\n');
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("set_io"))
            {
                var parts = trimmedLine.Split(' ');
                if (parts.Length != 3)
                {
                    ContainerLocator.Container.Resolve<ILogger>().Warning("PCF Line invalid: " + trimmedLine);
                    continue;
                }

                var signal = parts[1];
                var pin = parts[2];

                if (fpga.Pins.TryGetValue(pin, out var pinModel) && fpga.Nodes.TryGetValue(signal, out var signalModel))
                {
                    fpga.Connect(pinModel, signalModel);
                } 
            }
        }
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
            pcf = existingPcf.Trim();
        }

        foreach (var conn in SelectedFpga.Pins.Where(x => x.Value.Connection is not null))
        {
            pcf += $"\nset_io {conn.Value.Connection!.Name} {conn.Value.Name}";
        }
        pcf = pcf.Trim() + '\n';
            
        File.WriteAllText(pcfPath, pcf);
    }
}