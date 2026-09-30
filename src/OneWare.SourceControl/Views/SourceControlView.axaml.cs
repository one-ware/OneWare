using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LibGit2Sharp;
using OneWare.SourceControl.Models;
using OneWare.SourceControl.ViewModels;

namespace OneWare.SourceControl.Views;

public partial class SourceControlView : UserControl
{
    private const int GraphRow = 3;
    private GridLength _graphHeight = new(1, GridUnitType.Star);

    public SourceControlView()
    {
        InitializeComponent();

        ChangeListBox.DoubleTapped += OnChangeDoubleTap;
        StagedChangeListBox.DoubleTapped += OnStagedChangeDoubleTap;
        MergeChangeListBox.DoubleTapped += OnMergeChangeDoubleTap;

        GraphListBox.AddHandler(TappedEvent, OnGraphTapped, RoutingStrategies.Bubble, true);
        GraphListBox.AddHandler(KeyDownEvent, OnGraphKeyDown, RoutingStrategies.Bubble, true);
        GraphListBox.TemplateApplied += (_, args) =>
        {
            if (args.NameScope.Find<ScrollViewer>("PART_ScrollViewer") is { } scrollViewer)
                scrollViewer.ScrollChanged += OnGraphScrollChanged;
        };

        GraphSection.PropertyChanged += (_, args) =>
        {
            if (args.Property == Expander.IsExpandedProperty || args.Property == IsVisibleProperty)
                UpdateGraphRow();
        };
        UpdateGraphRow();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as SourceControlViewModel)?.AttachTopLevel(TopLevel.GetTopLevel(this));
    }

    /// <summary>
    ///     The graph shares the height with the changes only while it is expanded, the splitter keeps its size.
    /// </summary>
    private void UpdateGraphRow()
    {
        var row = RootGrid.RowDefinitions[GraphRow];
        var expanded = GraphSection.IsVisible && GraphSection.IsExpanded;

        if (!row.Height.IsAuto) _graphHeight = row.Height;
        row.Height = expanded ? _graphHeight : GridLength.Auto;
        row.MinHeight = expanded ? 80 : 0;
    }

    private void OnGraphScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || DataContext is not SourceControlViewModel vm) return;
        if (scrollViewer.Offset.Y + scrollViewer.Viewport.Height >= scrollViewer.Extent.Height - 200)
            _ = vm.LoadMoreHistoryAsync();
    }

    private void OnGraphTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not SourceControlViewModel vm) return;

        foreach (var visual in (e.Source as Visual)?.GetSelfAndVisualAncestors() ?? [])
        {
            if (visual == GraphListBox || visual is Button) return;
            if (visual is not Control control) continue;

            if (control.Classes.Contains("CommitFile") && control.DataContext is GitCommitFileModel file)
            {
                vm.CompareCommitFile(file);
                return;
            }

            if (control.Classes.Contains("CommitHeader") && control.DataContext is GitGraphRow row)
            {
                vm.ToggleCommit(row);
                return;
            }
        }
    }

    private void OnGraphKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space) || DataContext is not SourceControlViewModel vm ||
            GraphListBox.SelectedItem is not GitGraphRow row) return;
        vm.ToggleCommit(row);
        e.Handled = true;
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
