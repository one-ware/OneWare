using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectEntry : ObservableObject, IProjectEntry
{
    private string _name;

    protected ProjectEntry(string name, IProjectFolder? topFolder)
    {
        _name = name;
        TopFolder = topFolder;
    }

    public ObservableCollection<IProjectExplorerNode>? Children { get; protected set; } = new();

    public IProjectExplorerNode? Parent => TopFolder;

    public IProjectFolder? TopFolder { get; set; }

    public IBrush Background
    {
        get;
        set => SetProperty(ref field, value);
    } = Brushes.Transparent;

    public FontWeight FontWeight
    {
        get;
        set => SetProperty(ref field, value);
    } = FontWeight.Regular;

    public float TextOpacity
    {
        get;
        set => SetProperty(ref field, value);
    } = 1f;

    public IconModel? Icon
    {
        get;
        protected set => SetProperty(ref field, value);
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
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value)
                {
                    IsExpanded = false;
                    Icon?.AddOverlay("LoadingFailed", "VsImageLib.StatusCriticalErrorOverlayExp16X");
                }
                else
                {
                    Icon?.RemoveOverlay("LoadingFailed");
                }
            }
        }
    }

    public bool IsExpanded
    {
        get;
        set
        {
            if (this is IProjectFolder { Children.Count: 0 }) value = false;
            if (SetProperty(ref field, value))
            {
                OnIsExpandedChanged(value);
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

    public virtual void OnIsExpandedChanged(bool isExpanded)
    {
    }
}
