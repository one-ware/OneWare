using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectEntry : ObservableObject, IProjectEntry
{
    private IBrush _background = Brushes.Transparent;

    private FontWeight _fontWeight = FontWeight.Regular;

    private IImage? _icon;

    private bool _isExpanded;

    private bool _loadingFailed;

    private string _name;

    private float _textOpacity = 1f;
    
    private readonly IImage _loadingFailedOverlay;

    protected ProjectEntry(string name, IProjectFolder? topFolder)
    {
        _name = name;
        TopFolder = topFolder;
        
        _loadingFailedOverlay =
            Application.Current!.FindResource(ThemeVariant.Dark, "VsImageLib.StatusCriticalErrorOverlayExp16X") as IImage
            ?? throw new NullReferenceException(nameof(Application));
    }

    public ObservableCollection<IProjectExplorerNode> Children { get; } = new();
    public ObservableCollection<IProjectEntry> Entities { get; } = new();

    public IProjectExplorerNode? Parent => TopFolder;

    public IProjectFolder? TopFolder { get; set; }

    public ObservableCollection<IImage> IconOverlays { get; } = new();

    public ObservableCollection<IImage> RightIcons { get; } = new();

    public IBrush Background
    {
        get => _background;
        set => SetProperty(ref _background, value);
    }

    public FontWeight FontWeight
    {
        get => _fontWeight;
        set => SetProperty(ref _fontWeight, value);
    }

    public float TextOpacity
    {
        get => _textOpacity;
        set => SetProperty(ref _textOpacity, value);
    }

    public IImage? Icon
    {
        get => _icon;
        protected set => SetProperty(ref _icon, value);
    }

    public string Header => Name;

    public string Name
    {
        get => _name;
        set
        {
            SetProperty(ref _name, value);
            OnPropertyChanged(nameof(Header));
            OnPropertyChanged(nameof(FullPath));
            OnPropertyChanged(nameof(RelativePath));
        }
    }

    public bool LoadingFailed
    {
        get => _loadingFailed;
        set
        {
            SetProperty(ref _loadingFailed, value);
            if (value)
            {
                IconOverlays.Add(_loadingFailedOverlay);
                IsExpanded = false;
            }
            else
            {
                IconOverlays.Remove(_loadingFailedOverlay);
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value != _isExpanded)
            {
                //if (this is IProjectFolder { Children.Count: 0 }) value = false;
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

    public bool IsValid()
    {
        return true;
    }
}