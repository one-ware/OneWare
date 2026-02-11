using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Media;
using Avalonia.Styling;
using DynamicData;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ProjectExplorer.ViewModels;

public abstract class ProjectViewModelBase : ExtendedTool
{
    private string _searchString = "";
    private IEnumerable<MenuItemModel>? _treeViewContextMenu;

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

    public IEnumerable<MenuItemModel>? TreeViewContextMenu
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

    public HierarchicalTreeDataGridSource<IProjectExplorerNode> Source { get; }


    public virtual void AddProject(IProjectRoot entry)
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
        if (node.Parent?.Children != null)
        {
            indexPath.Add(node.Parent.Children.IndexOf(node));
            indexPath.AddRange(GetIndexPathFromNode(node.Parent));
        }

        return indexPath;
    }

    public void OnSearch()
    {
        ClearSelection();
        if (string.IsNullOrWhiteSpace(SearchString)) return;

        var searchText = SearchString.Trim();
        if (searchText.Length < 3) return;

        var bestScore = -1;
        IProjectRoot? bestProject = null;
        string? bestRelativePath = null;
        var comparison = StringComparison.OrdinalIgnoreCase;

        foreach (var project in Projects)
        {
            foreach (var relativePath in project.GetFiles())
            {
                if (!relativePath.Contains(searchText, comparison)) continue;

                var score = ScoreFileMatch(relativePath, searchText);
                if (score <= 0) continue;

                if (score > bestScore ||
                    (score == bestScore && bestRelativePath != null &&
                     ExplorerNameComparer.Instance.Compare(relativePath, bestRelativePath) < 0))
                {
                    bestScore = score;
                    bestProject = project;
                    bestRelativePath = relativePath;
                }
            }
        }

        if (bestProject == null || bestRelativePath == null) return;

        var file = bestProject.GetFile(bestRelativePath);
        if (file == null) return;

        ExpandToRoot(file);
        AddToSelection(file);
    }

    private static int ScoreFileMatch(string relativePath, string searchText)
    {
        var fileName = Path.GetFileName(relativePath);
        var comparison = StringComparison.OrdinalIgnoreCase;
        var score = 0;

        if (fileName.StartsWith(searchText, comparison)) score += 3;
        else if (fileName.Contains(searchText, comparison)) score += 2;
        else if (relativePath.Contains(searchText, comparison)) score += 1;

        return score;
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
