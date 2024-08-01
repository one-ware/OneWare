using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.Cyc5000.ViewModels;

public class Cyc5000ViewModel(FpgaModel model) : FpgaViewModelBase(model);