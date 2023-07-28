namespace OneWare.Terminal.Provider
{
    public interface IPseudoTerminalProvider
    {
        IPseudoTerminal? Create(int columns, int rows, string initialDirectory, string command, string? environment, string? arguments);
    }
}
