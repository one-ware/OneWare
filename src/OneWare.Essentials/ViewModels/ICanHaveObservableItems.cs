using System.Collections.ObjectModel;
using System.ComponentModel;

namespace OneWare.Essentials.ViewModels;

public interface ICanHaveObservableItems<T> : INotifyPropertyChanged, INotifyPropertyChanging
{
    public ObservableCollection<T>? Items { get; }
    
    public string Name { get; }
}