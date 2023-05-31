using Avalonia.Controls;
using Avalonia.Interactivity;
using LibGit2Sharp;
using OneWare.SourceControl.Models;
using OneWare.SourceControl.ViewModels;

namespace OneWare.SourceControl.Views
{
    public partial class SourceControlView : UserControl
    {
        private readonly ListBox _changeListBox, _stagedChangeListBox, _mergeChangeListBox;

        public SourceControlView()
        {
            InitializeComponent();

            _changeListBox = this.Find<ListBox>("ChangeListBox");
            _stagedChangeListBox = this.Find<ListBox>("StagedChangeListBox");
            _mergeChangeListBox = this.Find<ListBox>("MergeChangeListBox");

            _changeListBox.DoubleTapped += OnChangeDoubleTap;
            _stagedChangeListBox.DoubleTapped += OnStagedChangeDoubleTap;
            _mergeChangeListBox.DoubleTapped += OnMergeChangeDoubleTap;
        }

        public void OnChangeDoubleTap(object? sender, RoutedEventArgs e)
        {
            if (_changeListBox.SelectedItem is SourceControlModel scm) _ = AutoOpenAsync(scm);
        }

        public void OnStagedChangeDoubleTap(object? sender, RoutedEventArgs e)
        {
            if (_stagedChangeListBox.SelectedItem is SourceControlModel scm) _ = AutoOpenAsync(scm);
        }

        public void OnMergeChangeDoubleTap(object? sender, RoutedEventArgs e)
        {
            if (_mergeChangeListBox.SelectedItem is SourceControlModel scm) _ = AutoOpenAsync(scm);
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