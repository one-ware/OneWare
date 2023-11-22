using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.SDK;
using OneWare.SDK.Helpers;
using OneWare.SDK.Models;
using OneWare.SDK.Services;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectEntry : ObservableObject, IProjectEntry
{
    public ObservableCollection<IProjectEntry> Items { get; init; } = new();

    public IProjectFolder? TopFolder { get; set; }

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

    public float TextOpacity { get; } = 1f;

    private IImage? _icon;
    public IImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public ObservableCollection<IImage> IconOverlays { get; } = new();
    
    private string _header;
    public string Header
    {
        get => _header;
        set
        {
            SetProperty(ref _header, value);
            OnPropertyChanged(nameof(FullPath));
            OnPropertyChanged(nameof(RelativePath));
        }
    }

    private bool _excludeCompilation;
    public bool ExcludeCompilation
    {
        get => _excludeCompilation;
        set => SetProperty(ref _excludeCompilation, value);
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
                if (this is IProjectFolder { Items.Count: 0 }) value = false;
                SetProperty(ref _isExpanded, value);
            }
        }
    }

    public string RelativePath
    {
        get
        {
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

    protected ProjectEntry(string header, IProjectFolder? topFolder)
    {
        _header = header;
        TopFolder = topFolder;
    }
    
    public virtual IEnumerable<MenuItemModel> GetContextMenu(IProjectExplorerService projectExplorerService)
    {
        yield return new MenuItemModel("OpenFileViewer")
        {
            Header = "Open in File Viewer",
            Command = new RelayCommand(() => PlatformHelper.OpenExplorerPath(FullPath)),
            ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.OpenFolder16Xc"),
            Priority = 1000
        };
    }
    
    public bool IsValid()
    {
        return true;
    }
}