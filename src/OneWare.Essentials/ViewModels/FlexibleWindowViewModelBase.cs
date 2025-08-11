using Dock.Model.Mvvm.Controls;
using OneWare.Essentials.Controls;

namespace OneWare.Essentials.ViewModels;

public class FlexibleWindowViewModelBase : Document
{
    private bool _isDirty;

    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }


    /// <summary>
    /// This method is called when the window is closed.
    /// By default, it will cancel if the window is marked as dirty.
    /// It can be overridden to perform custom actions before the window is closed.
    /// Return true to allow the window to close, or false to cancel the close operation.
    /// *WARNING*: Do not call <see cref="Close(FlexibleWindow)"/> without setting IsDirty = false in this method, as it will cause an infinite loop.
    /// </summary>
    /// <param name="window">The owner window that wants to close</param>
    /// <returns>true to close the window, false to cancel</returns>
    public virtual bool OnWindowClosing(FlexibleWindow window)
    {
        return !IsDirty;
    }

    /// <summary>
    /// This method is called to close the window and can be bound in xaml.
    /// This is not overridable anymore, override <see cref="OnWindowClosing"/> instead.
    /// </summary>
    /// <param name="window"></param>
    public void Close(FlexibleWindow window)
    {
        window.Close();
    }
}