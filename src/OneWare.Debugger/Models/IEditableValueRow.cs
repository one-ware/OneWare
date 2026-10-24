namespace OneWare.Debugger.Models;

public interface IEditableValueRow
{
    public string Value { get; }

    public ValueEdit Edit { get; }
}
