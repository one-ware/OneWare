using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Runtime.Serialization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Prism.Ioc;

using OneWare.ProjectExplorer.ViewModels;
using OneWare.Shared;
using OneWare.Shared.Converters;
using OneWare.Shared.Services;

namespace OneWare.ProjectExplorer.Models;

public abstract class ProjectEntry : ObservableObject, IProjectEntry
{
    public ObservableCollection<IProjectEntry> Items { get; init; } = new();
    public DateTime LastSaveTime { get; set; } = DateTime.MinValue;
    
    public IProjectFolder TopFolder { get; set; }
    
    private IImage? _icon;
    public IImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }
    
    private string _header;
    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
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

    public RelayCommand<string> RequestRename { get; }

    protected ProjectEntry(string header, IProjectFolder topFolder)
    {
        _header = header;
        TopFolder = topFolder;
        
        Icon = SharedConverters.FileExtensionIconConverter.Convert(header, typeof(IImage), null, CultureInfo.CurrentCulture) as IImage;
        RequestRename = new RelayCommand<string>(Rename,(x) => LoadingFailed);
    }
    
    public bool IsValid()
    {
        return true;
    }
    
    public void Rename(string? newName)
    {
        if (newName == null) return;
        _ = ContainerLocator.Container.Resolve<IProjectService>().RenameAsync(this, newName);
    }
}