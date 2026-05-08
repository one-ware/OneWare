using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Context;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.ViewModels;

public class IcarusVerilogSimulatorToolbarViewModel : ObservableObject
{
    private readonly TestBenchContext _context;

    public IcarusVerilogSimulatorToolbarViewModel(TestBenchContext context, IFpgaSimulator simulator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _ = simulator ?? throw new ArgumentNullException(nameof(simulator));
    }

    public string IcarusVerilogArguments
    {
        get => _context.GetBenchProperty(nameof(IcarusVerilogArguments)) ?? "";
        set
        {
            _context.SetBenchProperty(nameof(IcarusVerilogArguments), value);
            OnPropertyChanged();
        }
    }
    
    public string[] AvailableWaveOutputFormats => ["VCD", "LXT2", "FST"];
    
    public string WaveOutputFormat
    {
        get => _context.GetBenchProperty(nameof(WaveOutputFormat)) ?? AvailableWaveOutputFormats[0];
        set
        {
            _context.SetBenchProperty(nameof(WaveOutputFormat), value);
            OnPropertyChanged();
        }
    }
}