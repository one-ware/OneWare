using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IWelcomeScreenItem : INotifyPropertyChanged
{
    /// <summary>
    /// Display name of the item.
    /// </summary>
    string Name { get; }
    /// <summary>
    /// Optional icon for the item.
    /// </summary>
    IconModel? Icon { get; }
    /// <summary>
    /// Command executed when the item is activated.
    /// </summary>
    ICommand? Command { get; }
}

public interface IWelcomeScreenService
{
    /// <summary>
    /// Registers an item under the "New" section.
    /// </summary>
    void RegisterItemToNew(string id, IWelcomeScreenStartItem item);
    /// <summary>
    /// Registers an item under the "Open" section.
    /// </summary>
    void RegisterItemToOpen(string id, IWelcomeScreenStartItem item);
    /// <summary>
    /// Registers an item under the "Walkthrough" section.
    /// </summary>
    void RegisterItemToWalkthrough(string id, IWelcomeScreenWalkthroughItem item);
}
