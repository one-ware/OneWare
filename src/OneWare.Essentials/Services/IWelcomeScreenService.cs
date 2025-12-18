using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Services;

public interface IWelcomeScreenItem : INotifyPropertyChanged
{
    string Name { get; }
    IImage Icon { get; }
    ICommand? Command { get; }
}

public interface IWelcomeScreenService
{
    void RegisterItemToNew(string id, IWelcomeScreenStartItem item);
    void RegisterItemToOpen(string id, IWelcomeScreenStartItem item);
    void RegisterItemToWalkthrough(string id, IWelcomeScreenWalkthroughItem item);
}