using Avalonia.Controls;
using Avalonia.Interactivity;
using LibGit2Sharp;
using OneWare.SourceControl.Models;
using OneWare.SourceControl.ViewModels;

namespace OneWare.SourceControl.Views;

public partial class SourceControlView : UserControl
{
    public SourceControlView()
    {
        InitializeComponent();

        ChangeListBox.DoubleTapped += OnChangeDoubleTap;
        StagedChangeListBox.DoubleTapped += OnStagedChangeDoubleTap;
        MergeChangeListBox.DoubleTapped += OnMergeChangeDoubleTap;
    }

    public void OnChangeDoubleTap(object? sender, RoutedEventArgs e)
    {
        if (ChangeListBox.SelectedItem is SourceControlFileModel scm) _ = AutoOpenAsync(scm);
    }

    public void OnStagedChangeDoubleTap(object? sender, RoutedEventArgs e)
    {
        if (StagedChangeListBox.SelectedItem is SourceControlFileModel scm && DataContext is SourceControlViewModel vm)
            vm.CompareStagedAndSwitch(scm.Status.FilePath);
    }

    public void OnMergeChangeDoubleTap(object? sender, RoutedEventArgs e)
    {
        if (MergeChangeListBox.SelectedItem is SourceControlFileModel scm) _ = AutoOpenAsync(scm);
    }

    public async Task AutoOpenAsync(SourceControlFileModel scm)
    {
        if (DataContext is not SourceControlViewModel vm || vm.IsLoading) return;
        if (scm.Status.State.HasFlag(FileStatus.Conflicted)) await vm.OpenFileAsync(scm.Status.FilePath);
        else vm.CompareAndSwitch(scm.Status.FilePath);
    }
}