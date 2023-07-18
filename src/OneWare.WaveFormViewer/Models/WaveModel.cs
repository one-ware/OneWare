using Avalonia.Media;

namespace OneWare.WaveFormViewer.Models;

public class WaveModel
{
    public string Label { get; }
    public IBrush WaveBrush { get; }

    public WaveModel(string label, IBrush waveBrush)
    {
        Label = label;
        WaveBrush = waveBrush;
    }
}