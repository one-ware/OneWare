using Avalonia.Media;
using OneWare.WaveFormViewer.Enums;

namespace OneWare.WaveFormViewer.Models;

public class WaveModel
{
    public string Label { get; }
    public IBrush WaveBrush { get; }
    public SignalLineType Type => SignalLineType.Reg;
    public SignalDataType DataType => SignalDataType.Binary;
    
    public WavePart[] Line { get; }

    public WaveModel(string label, IBrush waveBrush)
    {
        Label = label;
        WaveBrush = waveBrush;
    }
}