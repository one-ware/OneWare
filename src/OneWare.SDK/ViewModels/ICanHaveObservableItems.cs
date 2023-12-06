using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.SDK.ViewModels;

public interface ICanHaveObservableItems<T> : INotifyPropertyChanged, INotifyPropertyChanging
{
    public ObservableCollection<T>? Items { get; }
    
    public string Name { get; }
}