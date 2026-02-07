using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Media;
using Avalonia.Styling;
using DynamicData;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ProjectExplorer.ViewModels;

public abstract class ProjectViewModelBase : ExtendedTool
{
    private string _searchString = "";
    private IEnumerable<MenuItemViewModel>? _treeViewContextMenu;

    public ProjectViewModelBase(string iconKey) : base(iconKey)
    {
        Source = new HierarchicalTreeDataGridSource<IProjectExplorerNode>(Projects)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<IProjectExplorerNode>(
                    new TemplateColumn<IProjectExplorerNode>("Header", "ProjectExplorerColumnTemplate", null,
                        GridLength.Star), x => x.Children)
            }
        };

        Source.RowSelection!.SingleSelect = false;
        SelectedItems = Source.RowSelection.SelectedItems!;
    }

    public IEnumerable<MenuItemViewModel>? TreeViewContextMenu
    {
        get => _treeViewContextMenu;
        set => SetProperty(ref _treeViewContextMenu, value);
    }

    public string SearchString
    {
        get => _searchString;
        set => SetProperty(ref _searchString, value);
    }

    public ObservableCollection<IProjectRoot> Projects { get; } = new();

    public IReadOnlyList<IProjectExplorerNode> SelectedItems { get; }

    public ObservableCollection<IProjectExplorerNode> SearchResult { get; } = new();

    public HierarchicalTreeDataGridSource<IProjectExplorerNode> Source { get; }


    public virtual void Insert(IProjectRoot entry)
    {
        if (Projects.Any(x => x.FullPath.EqualPaths(entry.FullPath)))
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error("Project already loaded");
            return;
        }

        for (var i = 0; i < Projects.Count; i++)
            if (string.CompareOrdinal(entry.Header, Projects[i].Header) <= 0)
            {
                Projects.Insert(i, entry);
                return;
            }

        Projects.Add(entry);
    }

    #region Searching, Sort and Binding

    public void ResetSearch()
    {
    }

    public void ClearSelection()
    {
        Source.RowSelection?.Clear();
    }

    public void AddToSelection(IProjectExplorerNode node)
    {
        List<int>? indexPath = null;
        if (node.Parent == null)
            indexPath = [0];

        try
        {
            indexPath = GetIndexPathFromNode(node);
            if (indexPath.Count == 0)
                return;

            indexPath.Reverse();
            Source.RowSelection?.Select(new IndexPath(indexPath));
        }
        catch
        {
        }
    }

    private List<int> GetIndexPathFromNode(IProjectExplorerNode node)
    {
        List<int> indexPath = [];
        if (node.Parent != null)
        {
            indexPath.Add(node.Parent.Children.IndexOf(node));
            indexPath.AddRange(GetIndexPathFromNode(node.Parent));
        }

        return indexPath;
    }

    public void OnSearch()
    {
        ClearSelection();
        ResetSearch();
        foreach (var s in SearchResult) s.Background = Brushes.Transparent;
        SearchResult.Clear();
        if (SearchString.Length < 3) return;

        SearchResult.AddRange(DeepSearchName(SearchString));

        foreach (var r in SearchResult)
            r.Background = Application.Current?.FindResource(ThemeVariant.Dark, "SearchResultBrush") as IBrush ??
                           Brushes.Transparent;
    }

    public void ExpandToRoot(IProjectExplorerNode node)
    {
        if (node.Parent == null || node is IProjectRoot) return;
        node.Parent.IsExpanded = true;
        ExpandToRoot(node.Parent);
    }

    public IProjectRoot? GetRootFromFile(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return null;

        fullPath = Path.GetFullPath(fullPath);

        return (from project in Projects
            let projectRoot = Path.GetFullPath(project.RootFolderPath)
            where fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
            select project).FirstOrDefault();
    }

    public IProjectEntry? GetEntry(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        relativePath = relativePath.Replace('\\', '/');

        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        var projectRootName = parts[0];

        var project = Projects.FirstOrDefault(p =>
            Path.GetFileName(p.RootFolderPath)
                .Equals(projectRootName, StringComparison.OrdinalIgnoreCase));

        return project?.GetEntry(relativePath.ToPlatformPath());
    }

    public IProjectEntry? GetEntryFromFullPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return null;

        fullPath = Path.GetFullPath(fullPath);

        foreach (var project in Projects)
        {
            var projectRoot = Path.GetFullPath(project.RootFolderPath);

            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(projectRoot, fullPath);

            return project.GetEntry(relative);
        }

        return null;
    }

    public List<IProjectEntry> DeepSearchName(string name)
    {
        var results = new List<IProjectEntry>();
        foreach (var entry in Projects)
        {
            if (entry.Header.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) results.Add(entry);
            if (entry is IProjectFolder folder) DeepSearchName(folder, name, results);
        }

        return results;
    }

    private void DeepSearchName(IProjectFolder pf, string name, List<IProjectEntry> results)
    {
        var folderItems = pf.Entities;
        foreach (var entry in folderItems)
        {
            if (entry.Header.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) results.Add(entry);
            if (entry is IProjectFolder folder) DeepSearchName(folder, name, results);
        }
    }

    public void CollapseAll(IEnumerable<IProjectExplorerNode> list)
    {
        foreach (var f in list)
        {
            f.IsExpanded = false;
            CollapseAll(f.Children);
        }
    }

    #endregion
}