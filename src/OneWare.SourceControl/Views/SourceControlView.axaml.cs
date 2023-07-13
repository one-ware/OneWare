using Avalonia.Controls;
using Avalonia.Interactivity;
using LibGit2Sharp;
using OneWare.SourceControl.Models;
using OneWare.SourceControl.ViewModels;

namespace OneWare.SourceControl.Views
{
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
            if (ChangeListBox.SelectedItem is SourceControlModel scm) _ = AutoOpenAsync(scm);
        }

        public void OnStagedChangeDoubleTap(object? sender, RoutedEventArgs e)
        {
            if (StagedChangeListBox.SelectedItem is SourceControlModel scm) _ = AutoOpenAsync(scm);
        }

        public void OnMergeChangeDoubleTap(object? sender, RoutedEventArgs e)
        {
            if (MergeChangeListBox.SelectedItem is SourceControlModel scm) _ = AutoOpenAsync(scm);
        }

        public async Task AutoOpenAsync(SourceControlModel scm)
        {
            if (!(DataContext is SourceControlViewModel vm)) return;
            if (scm.Status.State == FileStatus.ModifiedInWorkdir || scm.Status.State == FileStatus.ModifiedInIndex)
                vm.CompareAndSwitch(scm.Status.FilePath);
            else if (scm.Status.State == FileStatus.DeletedFromWorkdir) await vm.OpenHeadFileAsync(scm.Status.FilePath);
            else if (scm.Status.State == FileStatus.NewInWorkdir || scm.Status.State == FileStatus.NewInIndex)
                await vm.OpenFileAsync(scm.Status.FilePath);
            else if (scm.Status.State == FileStatus.Conflicted) await vm.OpenFileAsync(scm.Status.FilePath);
        }


    }
}