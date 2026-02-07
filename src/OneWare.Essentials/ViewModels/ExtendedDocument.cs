using System.Runtime.Serialization;
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

    protected ExtendedDocument(string fullPath, IProjectExplorerService projectExplorerService,
        IMainDockService mainDockService, IWindowService windowService)
    {
        _fullPath = fullPath;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
        _windowService = windowService;
    }

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
        get;
        private set => SetProperty(ref field, value);
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

    public override bool OnClose()
    {
        if (IsDirty)
        {
            if (CurrentFile != null) _ = _mainDockService.CloseFileAsync(CurrentFile);
            return false;
        }

        if (CurrentFile != null) _mainDockService.OpenFiles.Remove(CurrentFile.FullPath.ToPathKey());
        if (CurrentFile is ExternalFile externalFile)
            _projectExplorerService.RemoveTemporaryFile(externalFile);

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
        var oldCurrentFile = CurrentFile;

        CurrentFile = _projectExplorerService.GetEntryFromFullPath(FullPath) as IFile ??
                      _projectExplorerService.GetTemporaryFile(FullPath);
        Title = CurrentFile is ExternalFile ? $"[{CurrentFile.Name}]" : CurrentFile.Name;

        if (CurrentFile != oldCurrentFile && oldCurrentFile != null)
            _mainDockService.OpenFiles.Remove(oldCurrentFile.FullPath.ToPathKey());

        _mainDockService.OpenFiles.TryAdd(CurrentFile.FullPath.ToPathKey(), this);

        UpdateCurrentFile(oldCurrentFile);
    }

    public virtual void GoToDiagnostic(ErrorListItem item)
    {
    }

    protected virtual void Reset()
    {
    }

    protected abstract void UpdateCurrentFile(IFile? oldFile);
}
