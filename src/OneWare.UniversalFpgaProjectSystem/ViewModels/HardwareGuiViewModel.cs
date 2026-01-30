using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class HardwareGuiViewModel : ObservableObject
{
    private readonly List<FpgaGuiElementViewModelBase> _elements = [];

    public HardwareGuiViewModel()
    {
        Elements = _elements.AsReadOnly();
    }

    public IReadOnlyList<FpgaGuiElementViewModelBase> Elements { get; }

    public int Width { get; set; }

    public int Height { get; set; }

    public Thickness Margin { get; set; }

    public void AddElement(FpgaGuiElementViewModelBase element)
    {
        _elements.Add(element);
    }

    public void Initialize()
    {
        foreach (var element in Elements) element.Initialize();
    }
}