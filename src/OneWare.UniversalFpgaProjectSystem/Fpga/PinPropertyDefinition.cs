namespace OneWare.UniversalFpgaProjectSystem.Fpga;

/// <summary>
/// Describes the type of control used to edit a per-pin property.
/// </summary>
public enum PinPropertyType
{
    /// <summary>Free-form text entry.</summary>
    Text,

    /// <summary>Dropdown with a fixed set of allowed values.</summary>
    ComboBox
}

/// <summary>
/// Declares a per-pin property that a hardware support module (e.g. Quartus Agilex toolchain)
/// wants to expose in the pin planner UI and persist via
/// <see cref="IFpgaToolchain.SaveConnections"/> / <see cref="IFpgaToolchain.LoadConnections"/>.
/// </summary>
/// <param name="Key">Unique identifier used as the dictionary key in <c>HardwarePinModel.PinPropertyValues</c>.</param>
/// <param name="DisplayName">Column header shown in the pin planner table.</param>
/// <param name="Type">Whether the cell renders as a text box or a combo box.</param>
/// <param name="AllowedValues">Allowed options when <paramref name="Type"/> is <see cref="PinPropertyType.ComboBox"/>.</param>
public record PinPropertyDefinition(
    string Key,
    string DisplayName,
    PinPropertyType Type = PinPropertyType.Text,
    string[]? AllowedValues = null);

