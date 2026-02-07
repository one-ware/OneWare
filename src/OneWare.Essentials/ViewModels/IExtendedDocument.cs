using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.ViewModels;

public interface IExtendedDocument : IDocument, IWaitForContent
{
    public IImage? Icon { get; }
    public string Extension { get; }
    public string FullPath { get; set; }
    public bool IsLoading { get; }
    public bool LoadingFailed { get; }
    public bool IsReadOnly { get; set; }
    public bool IsDirty { get; }
    public IRelayCommand? Undo { get; }
    public IRelayCommand? Redo { get; }
    public DateTime LastSaveTime { get; set; }
    public Task<bool> TryCloseAsync();
    public Task<bool> SaveAsync();
    public void GoToDiagnostic(ErrorListItem item);
}
