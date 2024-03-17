using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectEntry : ObservableObject, IProjectEntry
{
    public ObservableCollection<IProjectExplorerNode> Children { get; } = new();
    public ObservableCollection<IProjectEntry> Entities { get; } = new();

    public IProjectExplorerNode? Parent => TopFolder;
    
    public IProjectFolder? TopFolder { get; set; }
    
    public ObservableCollection<IImage> IconOverlays { get; } = new();

    public ObservableCollection<IImage> RightIcons { get; } = new();

    private IBrush _background = Brushes.Transparent;
    public IBrush Background
    {
        get => _background;
        set => SetProperty(ref _background, value);
    }
    
    private FontWeight _fontWeight = FontWeight.Regular;
    public FontWeight FontWeight
    {
        get => _fontWeight;
        set => SetProperty(ref _fontWeight, value);
    }

    private float _textOpacity = 1f;
    
    public float TextOpacity
    {
        get => _textOpacity;
        set => SetProperty(ref _textOpacity, value);
    }

    private IImage? _icon;
    public IImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string Header => Name;
    
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            SetProperty(ref _name, value);
            OnPropertyChanged(nameof(FullPath));
            OnPropertyChanged(nameof(RelativePath));
        }
    }

    private bool _loadingFailed;
    public bool LoadingFailed
    {
        get => _loadingFailed;
        set
        {
            SetProperty(ref _loadingFailed, value);
            if(value) IsExpanded = false;
        }
    }
        
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value != _isExpanded)
            {
                if (this is IProjectFolder { Children.Count: 0 }) value = false;
                SetProperty(ref _isExpanded, value);
            }
        }
    }

    public string RelativePath
    {
        get
        {
            if (this is IProjectRoot) return string.Empty;
            
            var relativePath = Header;
            var tFolder = TopFolder;
            while (tFolder is not (IProjectRoot or null))
            {
                relativePath = Path.Combine(tFolder.Header, relativePath);
                tFolder = tFolder.TopFolder;
            }

            return relativePath;
        }
    }

    public virtual string FullPath => Path.Combine(Root.RootFolderPath, RelativePath);
    
    public IProjectRoot Root
    {
        get
        {
            var tFolder = TopFolder;
            while (tFolder != null)
            {
                if (tFolder is ProjectRoot root) return root;
                tFolder = tFolder.TopFolder;
            }

            throw new NullReferenceException(nameof(Root));
        }
    }
    
    public Action<Action<string>>? RequestRename { get; set; }

    protected ProjectEntry(string name, IProjectFolder? topFolder)
    {
        _name = name;
        TopFolder = topFolder;
    }
    
    public virtual IEnumerable<MenuItemViewModel> GetContextMenu(IProjectExplorerService projectExplorerService)
    {
        yield return new MenuItemViewModel("OpenFileViewer")
        {
            Header = "Open in File Viewer",
            Command = new RelayCommand(() => PlatformHelper.OpenExplorerPath(FullPath)),
            IconObservable = Application.Current!.GetResourceObservable("VsImageLib.OpenFolder16Xc"),
            Priority = 1000
        };
    }
    
    public bool IsValid()
    {
        return true;
    }
}