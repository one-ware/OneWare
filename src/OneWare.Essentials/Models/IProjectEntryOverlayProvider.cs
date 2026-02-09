namespace OneWare.Essentials.Models;

public interface IProjectEntryOverlayProvider
{
    IEnumerable<IconLayer> GetOverlays(IProjectEntry entry);
}
