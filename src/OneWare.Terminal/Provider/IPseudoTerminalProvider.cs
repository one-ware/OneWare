namespace OneWare.Terminal.Provider
{
    public interface IPseudoTerminalProvider
    {
        IPseudoTerminal? Create(int columns, int rows, string initialDirectory, string? environment, string command, params string[] arguments);
    }
}
