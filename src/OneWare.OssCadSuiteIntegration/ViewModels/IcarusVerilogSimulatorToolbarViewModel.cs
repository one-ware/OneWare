using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Context;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.ViewModels;

public class IcarusVerilogSimulatorToolbarViewModel(TestBenchContext context, IFpgaSimulator simulator)
    : ObservableObject
{
    public string IcarusVerilogArguments
    {
        get => context.GetBenchProperty(nameof(IcarusVerilogArguments)) ?? "";
        set
        {
            context.SetBenchProperty(nameof(IcarusVerilogArguments), value);
            OnPropertyChanged();
        }
    }
}