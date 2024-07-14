using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Vcd.Parser.Data;
using OneWare.WaveFormViewer.Enums;
using Prism.Ioc;

namespace OneWare.WaveFormViewer.Models;

public partial class WaveModel : ObservableObject
{
    private bool _automaticFixedPointShift;

    private WaveDataType _dataType;

    private int _fixedPointShift;

    private string? _markerValue;

    public WaveModel(IVcdSignal signal, IBrush waveBrush)
    {
        Signal = signal;
        WaveBrush = waveBrush;

        if (signal.BitWidth == 1)
        {
            DataType = WaveDataType.Binary;
            AvailableDataTypes = [];
        }
        else
        {
            DataType = WaveDataType.SignedDecimal;
            AvailableDataTypes =
            [
                WaveDataType.Binary, WaveDataType.Decimal, WaveDataType.SignedDecimal, WaveDataType.Hex,
                WaveDataType.Ascii
            ];
        }
    }

    public string? MarkerValue
    {
        get => _markerValue;
        set => SetProperty(ref _markerValue, value);
    }

    public IBrush WaveBrush { get; }

    public WaveDataType[] AvailableDataTypes { get; }

    public WaveDataType DataType
    {
        get => _dataType;
        set => SetProperty(ref _dataType, value);
    }

    public bool AutomaticFixedPointShift
    {
        get => _automaticFixedPointShift;
        set
        {
            SetProperty(ref _automaticFixedPointShift, value);

            if (value)
                FixedPointShift = GetAutomaticFixedPointShift(Signal.Name);
            else
                FixedPointShift = 0;
        }
    }

    public int FixedPointShift
    {
        get => _fixedPointShift;
        set => SetProperty(ref _fixedPointShift, value);
    }

    public IVcdSignal Signal { get; }

    [GeneratedRegex(@"\[(-?\d+):(-?\d+)\]")]
    private static partial Regex FixedPointShiftRegex();

    public void SetDataType(WaveDataType dataType)
    {
        DataType = dataType;
    }

    public void ToggleAutomaticFixedPointShift()
    {
        AutomaticFixedPointShift = !AutomaticFixedPointShift;
    }

    public async Task SpecifyFixedPointShiftDialogAsync(Visual owner)
    {
        var result = await ContainerLocator.Container.Resolve<IWindowService>().ShowInputAsync(
            "Fixed point shift", "Specify fixed point shift", MessageBoxIcon.Info,
            FixedPointShift.ToString(), TopLevel.GetTopLevel(owner) as Window);

        if (result != null && int.TryParse(result, out var shift))
        {
            AutomaticFixedPointShift = false;
            FixedPointShift = shift;
        }
    }

    public static int GetAutomaticFixedPointShift(string name)
    {
        var regex = FixedPointShiftRegex();
        var match = regex.Match(name);

        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var lower) ||
            !int.TryParse(match.Groups[2].Value, out var upper)) return 0;
        if (upper > lower) return upper - lower;
        if (lower > upper) return upper * -1;
        return 0;
    }
}