using CommunityToolkit.Mvvm.ComponentModel;


namespace OneWare.Shared.Models;

public class TreeViewItemViewModel : ObservableObject
{
    private string _header;

    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    public TreeViewItemViewModel(string header)
    {
        _header = header;
    }
}