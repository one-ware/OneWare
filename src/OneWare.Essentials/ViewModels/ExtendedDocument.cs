using System.IO;
using System.Runtime.Serialization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.ViewModels;

public abstract class ExtendedDocument : Document, IExtendedDocument
{
    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;

    private string _fullPath;
    private string? _lastFullPath;

    protected ExtendedDocument(string fullPath, IProjectExplorerService projectExplorerService,
        IMainDockService mainDockService, IWindowService windowService)
    {
        _fullPath = fullPath;
        _lastFullPath = fullPath;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
        _windowService = windowService;
    }

    public virtual string CloseWarningMessage =>
        $"Do you want to save changes to the file {Path.GetFileName(FullPath)}?";
    public IRelayCommand? Undo { get; protected set; }
    public IRelayCommand? Redo { get; protected set; }
    public IImage? Icon
    {
        get;
        private set => SetProperty(ref field, value);
    }
    public string Extension => Path.GetExtension(FullPath);

    [DataMember]
    public string FullPath
    {
        get => _fullPath;
        set
        {
            SetProperty(ref _fullPath, value);
            Id = $"Document: {value}";
        }
    }

    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public bool LoadingFailed
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsReadOnly
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsDirty
    {
        get;
        set => SetProperty(ref field, value);
    }

    public DateTime LastSaveTime
    {
        get;
        set => SetProperty(ref field, value);
    }

    public override bool OnClose()
    {
        if (IsDirty)
        {
            _ = _mainDockService.CloseFileAsync(FullPath);
            return false;
        }

        _mainDockService.OpenFiles.Remove(FullPath.ToPathKey());
        _mainDockService.UnregisterOpenFile(FullPath);
        
        Reset();
        return true;
    }

    public virtual async Task<bool> TryCloseAsync()
    {
        if (!IsDirty) return true;

        var result = await _windowService.ShowYesNoCancelAsync("Warning", CloseWarningMessage, MessageBoxIcon.Warning,
            _mainDockService.GetWindowOwner(this));

        if (result == MessageBoxStatus.Yes)
        {
            if (await SaveAsync()) return true;
        }
        else if (result == MessageBoxStatus.No)
        {
            IsDirty = false;
            return true;
        }

        return false;
    }

    public virtual Task<bool> SaveAsync()
    {
        return Task.FromResult(true);
    }

    public virtual void InitializeContent()
    {
        var oldPath = _lastFullPath;
        var isExternal = _projectExplorerService.GetRootFromFile(FullPath) == null;
        
        Title = isExternal ? $"[{Path.GetFileName(FullPath)}]" : Path.GetFileName(FullPath);
        
        if (File.Exists(FullPath)) LastSaveTime = File.GetLastWriteTime(FullPath);

        if (!string.IsNullOrWhiteSpace(oldPath) && !oldPath.EqualPaths(FullPath))
            _mainDockService.OpenFiles.Remove(oldPath.ToPathKey());

        _mainDockService.OpenFiles.TryAdd(FullPath.ToPathKey(), this);

        _lastFullPath = FullPath;
        UpdateCurrentFile(oldPath);
    }

    public virtual void GoToDiagnostic(ErrorListItem item)
    {
    }

    protected virtual void Reset()
    {
    }

    protected abstract void UpdateCurrentFile(string? oldPath);
}
