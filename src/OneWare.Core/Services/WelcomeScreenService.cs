using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class WelcomeScreenService : IWelcomeScreenService
{
    private readonly IDictionary<string, IWelcomeScreenStartItem> _startItems =
        new Dictionary<string, IWelcomeScreenStartItem>();

    private readonly IDictionary<string, IWelcomeScreenWalkthroughItem> _walkthroughItems =
        new Dictionary<string, IWelcomeScreenWalkthroughItem>();

    private IWelcomeScreenReceiver? _receiver;

    public void RegisterItemToNew(string id, IWelcomeScreenStartItem item)
    {
        if (_receiver == null)
            throw new InvalidOperationException();

        _startItems.Add(id, item);
        _receiver.HandleRegisterItemToNew(item);
    }

    public void RegisterItemToOpen(string id, IWelcomeScreenStartItem item)
    {
        if (_receiver == null)
            throw new InvalidOperationException();

        _startItems.Add(id, item);
        _receiver.HandleRegisterItemToOpen(item);
    }

    public void RegisterItemToWalkthrough(string id, IWelcomeScreenWalkthroughItem item)
    {
        if (_receiver == null)
            throw new InvalidOperationException();

        _walkthroughItems.Add(id, item);
        _receiver.HandleRegisterItemToWalkthrough(item);
    }

    internal void RegisterReceiver(IWelcomeScreenReceiver receiver)
    {
        _receiver = receiver;
    }

    public bool StartItemIsRegistered(string id)
    {
        return _startItems.ContainsKey(id);
    }

    public bool WalkthroughItemIsRegistered(string id)
    {
        return _walkthroughItems.ContainsKey(id);
    }
}

internal interface IWelcomeScreenReceiver
{
    void HandleRegisterItemToNew(IWelcomeScreenStartItem item);
    void HandleRegisterItemToOpen(IWelcomeScreenStartItem item);
    void HandleRegisterItemToWalkthrough(IWelcomeScreenWalkthroughItem item);
}