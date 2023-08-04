namespace OneWare.Shared.Models;

public interface IProjectManager
{
    public Task<IProjectRoot?> LoadProjectAsync(string path);

    public Task<bool> SaveProjectAsync(IProjectRoot root);

    public IEnumerable<MenuItemModel> ConstructContextMenu(IProjectEntry entry);
}