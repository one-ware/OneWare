using OneWare.Shared;

namespace OneWare.ErrorList;

public class ErrorRefreshArgs : EventArgs
{
    public ErrorRefreshArgs(IHasPath? entry)
    {
        Entry = entry;
    }

    public IHasPath? Entry { get; }
}