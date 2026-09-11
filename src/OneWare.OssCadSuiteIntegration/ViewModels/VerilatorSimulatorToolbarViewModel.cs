using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Context;

namespace OneWare.OssCadSuiteIntegration.ViewModels;

public class VerilatorSimulatorToolbarViewModel(TestBenchContext context) : ObservableObject
{
    public string TopModule
    {
        get => context.GetBenchProperty(nameof(TopModule)) ?? Path.GetFileNameWithoutExtension(context.FilePath);
        set
        {
            context.SetBenchProperty(nameof(TopModule), value);
            OnPropertyChanged();
        }
    }

    public string VerilatorArguments
    {
        get => context.GetBenchProperty(nameof(VerilatorArguments)) ?? "";
        set
        {
            context.SetBenchProperty(nameof(VerilatorArguments), value);
            OnPropertyChanged();
        }
    }

    public string VerilatorRuntimeArguments
    {
        get => context.GetBenchProperty(nameof(VerilatorRuntimeArguments)) ?? "";
        set
        {
            context.SetBenchProperty(nameof(VerilatorRuntimeArguments), value);
            OnPropertyChanged();
        }
    }
}
