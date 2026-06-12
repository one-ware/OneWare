using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class HardwarePinModel : ObservableObject
{
    private FpgaNodeModel? _connectedNode;

    private bool _isSelected;

    private string _toolTipText;

    private readonly Dictionary<string, string> _pinPropertyValues;

    public HardwarePinModel(HardwarePin pin, FpgaModel fpgaModel)
    {
        Pin = pin;
        _toolTipText = "Click to connect " + Pin.Name;
        FpgaModel = fpgaModel;
        // Pre-populate from hardware-defined defaults in fpga.json
        _pinPropertyValues = new Dictionary<string, string>(pin.Properties);
    }

    public HardwarePin Pin { get; }

    public FpgaModel FpgaModel { get; }

    /// <summary>
    /// Runtime-editable per-pin property values (e.g. IO Voltage).
    /// Keys correspond to <see cref="PinPropertyDefinition.Key"/> values declared by the active toolchain.
    /// Populated with hardware defaults from <see cref="HardwarePin.Properties"/> on construction
    /// and overwritten by <see cref="IFpgaToolchain.LoadConnections"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> PinPropertyValues => _pinPropertyValues;

    /// <summary>Fired whenever a per-pin property value changes.</summary>
    public event EventHandler? PinPropertyChanged;

    /// <summary>Returns the current value for <paramref name="key"/>, or an empty string if absent.</summary>
    public string GetPinPropertyValue(string key) =>
        _pinPropertyValues.TryGetValue(key, out var v) ? v : string.Empty;

    /// <summary>
    /// Updates the value for <paramref name="key"/> and raises <see cref="PinPropertyChanged"/>.
    /// Passing <c>null</c> or an empty string removes the key from the dictionary.
    /// </summary>
    public void SetPinPropertyValue(string key, string? value)
    {
        var existing = GetPinPropertyValue(key);
        var newValue = value ?? string.Empty;
        if (existing == newValue) return;

        if (string.IsNullOrEmpty(newValue))
            _pinPropertyValues.Remove(key);
        else
            _pinPropertyValues[key] = newValue;

        PinPropertyChanged?.Invoke(this, EventArgs.Empty);
    }

    public string ToolTipText
    {
        get => _toolTipText;
        set => SetProperty(ref _toolTipText, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public FpgaNodeModel? ConnectedNode
    {
        get => _connectedNode;
        set
        {
            SetProperty(ref _connectedNode, value);
            if (_connectedNode == null) ToolTipText = "Click to connect " + Pin.Name;
            else ToolTipText = Pin.Name + " is connected with " + _connectedNode.Node.Name;
        }
    }

    public override string ToString()
    {
        return ConnectedNode is null ? Pin.Name : $"{Pin.Name} <-> {ConnectedNode.Node.Name}";
    }
}