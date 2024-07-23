using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.ViewModels;

public abstract class ExtendedDocument : Document, IExtendedDocument
{
    private readonly IDockService _dockService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;

    private IFile? _currentFile;

    private string _fullPath;

    private bool _isDirty;

    private bool _isLoading = true;

    private bool _isReadOnly;

    private bool _loadingFailed;

    protected ExtendedDocument(string fullPath, IProjectExplorerService projectExplorerService,
        IDockService dockService, IWindowService windowService)
    {
        _fullPath = fullPath;
        _projectExplorerService = projectExplorerService;
        _dockService = dockService;
        _windowService = windowService;
    }

    public IAsyncRelayCommand? TryClose { get; protected set; }

    public virtual string CloseWarningMessage => $"Do you want to save changes to the file {CurrentFile?.Name}?";
    public IRelayCommand? Undo { get; protected set; }
    public IRelayCommand? Redo { get; protected set; }

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

    public IFile? CurrentFile
    {
        get => _currentFile;
        private set => SetProperty(ref _currentFile, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool LoadingFailed
    {
        get => _loadingFailed;
        set => SetProperty(ref _loadingFailed, value);
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetProperty(ref _isReadOnly, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        protected set => SetProperty(ref _isDirty, value);
    }

    public override bool OnClose()
    {
        if (IsDirty)
        {
            if (CurrentFile != null) _ = _dockService.CloseFileAsync(CurrentFile);
            return false;
        }

        if (CurrentFile != null) _dockService.OpenFiles.Remove(CurrentFile);
        if (CurrentFile is ExternalFile externalFile)
            _projectExplorerService.RemoveTemporaryFile(externalFile);

        Reset();
        return true;
    }

    public virtual async Task<bool> TryCloseAsync()
    {
        if (!IsDirty) return true;

        var result = await _windowService.ShowYesNoCancelAsync("Warning", CloseWarningMessage, MessageBoxIcon.Warning,
            _dockService.GetWindowOwner(this));

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
        var oldCurrentFile = CurrentFile;
        
        CurrentFile = _projectExplorerService.SearchFullPath(FullPath) as IFile ??
                      _projectExplorerService.GetTemporaryFile(FullPath);
        Title = CurrentFile is ExternalFile ? $"[{CurrentFile.Name}]" : CurrentFile.Name;

        if (CurrentFile != oldCurrentFile && oldCurrentFile != null)
        {
            _dockService.OpenFiles.Remove(oldCurrentFile);
        }
        
        _dockService.OpenFiles.TryAdd(CurrentFile, this);
        
        UpdateCurrentFile(oldCurrentFile);
    }

    protected virtual void Reset()
    {
    }

    protected abstract void UpdateCurrentFile(IFile? oldFile);
}