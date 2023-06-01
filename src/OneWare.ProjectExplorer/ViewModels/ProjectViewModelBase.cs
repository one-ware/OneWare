using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Dock.Model.Mvvm.Controls;
using OneWare.ProjectExplorer.Models;
using OneWare.Shared;
using OneWare.Shared.Extensions;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.ViewModels;

public abstract class ProjectViewModelBase : Tool
{
    public bool EnableDragDrop = true;

    private string _searchString = "";
    
    public string SearchString
    {
        get => _searchString;
        set => SetProperty(ref _searchString, value);
    }
    
    public ObservableCollection<IProjectEntry> Items { get; init; } = new();

    public ObservableCollection<IProjectEntry> SelectedItems { get; } = new();
    public IProjectEntry? SelectedItem => SelectedItems.Count > 0 ? SelectedItems[^1] : null;

    public ObservableCollection<IProjectEntry> SearchResult { get; } = new();

    public void Insert(ProjectEntry entry)
    {
        if (Items.Any(x => x.FullPath.EqualPaths(entry.FullPath)))
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error("Project already loaded");
            return;
        }
            
        //TODO
        var inserted = false;
        //FIND CORRECT INDEX IN ITEMS FOR INSERTION
        for (var i = 0; i < Items.Count; i++)
            if (entry is IProjectFolder || Items[i] is not IProjectFolder)
                if (entry is IProjectFolder && Items[i] is not IProjectFolder ||
                    string.CompareOrdinal(entry.Header, Items[i].Header) <= 0)
                {
                    Items.Insert(i, entry);
                    inserted = true;
                    break;
                }

        if (!inserted) Items.Add(entry);
    }

    #region Searching, Sort and Binding

    public void ResetSearch()
    {
        
    }

    public void OnSearch()
    {
        SelectedItems.Clear();
        ResetSearch();
        SearchResult.Clear();
        if (SearchString.Length < 3) return;

        SearchResult.AddRange(DeepSearchName(SearchString));
        
        CollapseAll(Items);

        foreach (var r in SearchResult)
        {
            ExpandToRoot(r);
        }
    }

    public void ExpandToRoot(IProjectEntry entry)
    {
        if (entry.TopFolder == null || entry is ProjectRoot) return;
        entry.TopFolder.IsExpanded = true;
        ExpandToRoot(entry.TopFolder);
    }

    public IProjectEntry? DeepSearch(string path)
    {
        foreach (var i in Items)
        {
            if (path.IndexOf(i.RelativePath, StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.Length == i.RelativePath.Length) return i;

            if (i is ProjectFolder folder)
            {
                var pe = folder.Search(path);
                if (pe != null) return pe;
            }
        }

        return null;
    }

    public IProjectEntry? Search(string path, bool recursive = true)
    {
        foreach (var i in Items)
        {
            if (Path.GetFullPath(path).Equals(Path.GetFullPath(i.Header),
                    StringComparison.OrdinalIgnoreCase) //Search for name equality
                || Path.GetFullPath(path).Equals(Path.GetFullPath(i.RelativePath),
                    StringComparison.OrdinalIgnoreCase) //Search for relative path equality
                || Path.GetFullPath(path).Equals(Path.GetFullPath(i.FullPath),
                    StringComparison.OrdinalIgnoreCase)) //Search for full path equality
                return i;
            if (recursive && i is ProjectFolder folder)
            {
                var pe = folder.Search(path);
                if (pe != null) return pe;
            }
        }

        return null;
    }

    public List<IProjectEntry> DeepSearchName(string name)
    {
        var results = new List<IProjectEntry>();
        foreach (var entry in Items)
        {
            if (entry.Header.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) results.Add(entry);
            if (entry is ProjectFolder folder) DeepSearchName(folder, name, results);
        }

        return results;
    }

    private void DeepSearchName(IProjectFolder pf, string name, List<IProjectEntry> results)
    {
        var folderItems = pf.Items;
        foreach (var entry in folderItems)
        {
            if (entry.Header.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) results.Add(entry);
            if (entry is ProjectFolder folder) DeepSearchName(folder, name, results);
        }
    }

    public void CollapseAll(IEnumerable<IProjectEntry> list)
    {
        foreach (var f in list.Where(x => x is ProjectFolder).Cast<ProjectFolder>())
        {
            f.IsExpanded = false;
            CollapseAll(f.Items);
        }
    }

    #endregion
}