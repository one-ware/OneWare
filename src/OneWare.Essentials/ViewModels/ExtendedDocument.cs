using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.ViewModels;

public abstract class ExtendedDocument : Document, IExtendedDocument
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;
    public IRelayCommand? Undo { get; protected set; }
    public IRelayCommand? Redo { get; protected set; }

    public IAsyncRelayCommand? TryClose { get; protected set; }

    private string _fullPath;

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

    private IFile? _currentFile;

    public IFile? CurrentFile
    {
        get => _currentFile;
        private set => SetProperty(ref _currentFile, value);
    }

    private bool _isLoading = true;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private bool _loadingFailed;

    public bool LoadingFailed
    {
        get => _loadingFailed;
        set => SetProperty(ref _loadingFailed, value);
    }

    private bool _isReadOnly;

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetProperty(ref _isReadOnly, value);
    }

    private bool _isDirty;

    public bool IsDirty
    {
        get => _isDirty;
        protected set => SetProperty(ref _isDirty, value);
    }

    public virtual string CloseWarningMessage => $"Do you want to save changes to the file {CurrentFile?.Name}?";

    protected ExtendedDocument(string fullPath, IProjectExplorerService projectExplorerService, IDockService dockService, IWindowService windowService)
    {
        _fullPath = fullPath;
        _projectExplorerService = projectExplorerService;
        _dockService = dockService;
        _windowService = windowService;
    }
    
    public override bool OnClose()
    {
        if (IsDirty)
        {
            if(CurrentFile != null) _ = _dockService.CloseFileAsync(CurrentFile);
            return false;
        }
        else
        {
            if(CurrentFile != null) _dockService.OpenFiles.Remove(CurrentFile);
            if(CurrentFile is ExternalFile externalFile) 
                _projectExplorerService.RemoveTemporaryFile(externalFile);
        }

        Reset();
        return true;
    }

    protected virtual void Reset()
    {
        
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
        if (CurrentFile?.FullPath != FullPath)
        {
            CurrentFile = _projectExplorerService.SearchFullPath(FullPath) as IFile ?? _projectExplorerService.GetTemporaryFile(FullPath);
            Title = CurrentFile is ExternalFile ? $"[{CurrentFile.Name}]" : CurrentFile.Name;
        }
        _dockService.OpenFiles.TryAdd(CurrentFile, this);
        UpdateCurrentFile(oldCurrentFile);
    }

    protected abstract void UpdateCurrentFile(IFile? oldFile);
}