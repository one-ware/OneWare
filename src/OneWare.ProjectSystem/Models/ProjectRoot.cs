using System.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectRoot : ProjectFolder, IProjectRoot
{
    private readonly List<IProjectEntryOverlayProvider> _overlayProviders = new();

    protected ProjectRoot(string rootFolderPath) : base(Path.GetFileName(rootFolderPath),
        null)
    {
        RootFolderPath = rootFolderPath;
        TopFolder = this;
        RegisterOverlayProvider(new LoadingFailedOverlayProvider());
    }

    public abstract string ProjectPath { get; }

    public abstract string ProjectTypeId { get; }
    public string RootFolderPath { get; }
    public override string FullPath => RootFolderPath;

    public bool IsActive
    {
        get;
        set
        {
            SetProperty(ref field, value);
            FontWeight = value ? FontWeight.Bold : FontWeight.Regular;
        }
    }

    public Task InitializeAsync()
    {
        return LoadContentAsync();
    }

    public abstract bool IsPathIncluded(string path);
    
    public abstract void IncludePath(string path);

    public virtual void OnExternalEntryAdded(string relativePath, FileAttributes attributes)
    {
        if (!IsPathIncluded(relativePath)) return;
        
        var parentPath = Path.GetDirectoryName(relativePath);
        if (parentPath != null && GetLoadedEntry(parentPath) is IProjectFolder folder)
        {
            if (folder.IsExpanded)
            {
                if (attributes.HasFlag(FileAttributes.Directory))
                    AddFolder(relativePath);
                else
                    AddFile(relativePath);
            }
        }
    }

    public event EventHandler<ProjectEntryOverlayChangedEventArgs>? EntryOverlaysChanged;

    public IReadOnlyList<IconLayer> GetEntryOverlays(IProjectEntry entry)
    {
        if (_overlayProviders.Count == 0) return Array.Empty<IconLayer>();

        var overlays = new List<IconLayer>();
        foreach (var provider in _overlayProviders)
            overlays.AddRange(provider.GetOverlays(entry));

        return overlays;
    }

    public void NotifyEntryOverlayChanged(IProjectEntry entry)
    {
        EntryOverlaysChanged?.Invoke(this, new ProjectEntryOverlayChangedEventArgs(entry));
    }

    public void RegisterOverlayProvider(IProjectEntryOverlayProvider provider)
    {
        if (!_overlayProviders.Contains(provider))
            _overlayProviders.Add(provider);
    }

    public void UnregisterOverlayProvider(IProjectEntryOverlayProvider provider)
    {
        _overlayProviders.Remove(provider);
    }

    private sealed class LoadingFailedOverlayProvider : IProjectEntryOverlayProvider
    {
        public IEnumerable<IconLayer> GetOverlays(IProjectEntry entry)
        {
            if (!entry.LoadingFailed) return [];

            return
            [
                new IconLayer("VsImageLib.StatusCriticalErrorOverlayExp16X")
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    SizeRatio = 1
                }
            ];
        }
    }
}
