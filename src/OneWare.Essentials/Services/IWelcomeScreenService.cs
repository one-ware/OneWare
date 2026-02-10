using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IWelcomeScreenItem : INotifyPropertyChanged
{
    string Name { get; }
    IconModel Icon { get; }
    ICommand? Command { get; }
}

public interface IWelcomeScreenService
{
    void RegisterItemToNew(string id, IWelcomeScreenStartItem item);
    void RegisterItemToOpen(string id, IWelcomeScreenStartItem item);
    void RegisterItemToWalkthrough(string id, IWelcomeScreenWalkthroughItem item);
}