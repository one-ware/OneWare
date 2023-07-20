using Avalonia.Media;
using OneWare.WaveFormViewer.Enums;

namespace OneWare.WaveFormViewer.Models;

public class WaveModel
{
    public string Label { get; }
    public IBrush WaveBrush { get; }
    public SignalLineType LineType { get; }
    public SignalDataType DataType => SignalDataType.Binary;
    public WavePart[] Line { get; }

    public WaveModel(string label, SignalLineType lineType, WavePart[] line, IBrush waveBrush)
    {
        Label = label;
        LineType = lineType;
        Line = line;
        WaveBrush = waveBrush;
    }
}