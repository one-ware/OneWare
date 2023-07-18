using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;



namespace OneWare.Shared;

public interface IExtendedDocument : IDocument, IWaitForContent
{
    public IFile? CurrentFile { get; }
    public IRelayCommand? Undo { get; }
    public IRelayCommand? Redo { get; }
    public string FullPath { get; set; }
    public bool IsLoading { get; }
    public bool LoadingFailed { get; }
    public bool IsReadOnly { get; set; }
    public bool IsDirty { get; }
    public Task<bool> TryCloseAsync();
    public Task<bool> SaveAsync();
}