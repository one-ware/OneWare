﻿using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ProjectExplorer.ViewModels;

public abstract class ProjectViewModelBase : ExtendedTool
{
    private readonly ILogger _logger;
    private string _searchString = "";
    public bool EnableDragDrop = true;
    private IEnumerable<MenuItemViewModel>? _treeViewContextMenu;

    public ProjectViewModelBase(string iconKey, ILogger logger)
        : base(iconKey)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    public ObservableCollection<IProjectExplorerNode> SelectedItems { get; } = new();

    public ObservableCollection<IProjectExplorerNode> SearchResult { get; } = new();

    public virtual void Insert(IProjectRoot entry)
    {
        if (Projects.Any(x => x.FullPath.EqualPaths(entry.FullPath)))
        {
            _logger?.Error("Project already loaded");
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

    public void OnSearch()
    {
        SelectedItems.Clear();
        ResetSearch();
        foreach (var s in SearchResult) s.Background = Brushes.Transparent;
        SearchResult.Clear();
        if (SearchString.Length < 3) return;

        SearchResult.AddRange(DeepSearchName(SearchString));

        CollapseAll(Projects);

        foreach (var r in SearchResult)
        {
            r.Background = Application.Current?.FindResource(ThemeVariant.Dark, "SearchResultBrush") as IBrush ??
                           Brushes.Transparent;
            ExpandToRoot(r);
        }
    }

    public void ExpandToRoot(IProjectExplorerNode node)
    {
        if (node.Parent == null || node is IProjectRoot) return;
        node.Parent.IsExpanded = true;
        ExpandToRoot(node.Parent);
    }

    public IProjectEntry? SearchName(string path, bool recursive = true)
    {
        foreach (var i in Projects)
        {
            if (path.Equals(Path.GetFullPath(i.Header), StringComparison.OrdinalIgnoreCase)) return i;
            if (recursive && i is IProjectFolder folder)
            {
                var pe = folder.SearchName(path);
                if (pe != null) return pe;
            }
        }

        return null;
    }

    public IProjectEntry? SearchFullPath(string path, bool recursive = true)
    {
        foreach (var i in Projects)
        {
            if (path.EqualPaths(i.FullPath)) //Search for name equality
                return i;
            if (recursive && i is IProjectFolder folder)
            {
                var pe = folder.SearchFullPath(path);
                if (pe != null) return pe;
            }
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
