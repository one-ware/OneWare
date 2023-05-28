using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Shared.ViewModels
{
    public class ViewModelBase : ObservableObject
    {
        public virtual void Close(Window window)
        {
            window.Close();
        }
    }
}