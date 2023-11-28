using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.SDK.ViewModels;

public class TreeViewItemViewModel : ObservableObject
{
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
    
    private string _header;
    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    private IEnumerable<TreeViewItemViewModel>? _children;
    public IEnumerable<TreeViewItemViewModel>? Children
    {
        get => _children;
        set => SetProperty(ref _children, value);
    }

    private TreeViewItemViewModel? _parent;
    public TreeViewItemViewModel? Parent
    {
        get => _parent;
        set => SetProperty(ref _parent, value);
    }

    public TreeViewItemViewModel(string header)
    {
        _header = header;
    }
}