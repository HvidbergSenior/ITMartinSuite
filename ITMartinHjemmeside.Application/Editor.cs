using ITMartinHjemmeside.Domain;

namespace ITMartinHjemmeside.Application;

/// <summary>
/// Changing a site's pages and boxes - the same use cases for Martin and for the owner (the Server checks who may).
/// Every change first keeps a copy of the site, so Fortryd can always take one step back, and nothing can be broken for good.
/// </summary>
public sealed class Editor(ISiteStore store, IPictureStore pictures, TimeProvider clock)
{
    private static readonly object Gate = new();

    public Page AddPage(string id, string title) => Change(id, s => s.AddPage(title));
    public void RenamePage(string id, string slug, string title) => Change(id, s => { s.RenamePage(slug, title); return 0; });
    public void ShowInMenu(string id, string slug, bool show) => Change(id, s => { s.ShowInMenu(slug, show); return 0; });
    public void MovePage(string id, string slug, int delta) => Change(id, s => { s.MovePage(slug, delta); return 0; });
    public void RemovePage(string id, string slug) => Change(id, s => { s.RemovePage(slug); return 0; });

    public Block AddText(string id, string slug, string text) => Change(id, s => s.AddText(slug, text));
    public void EditText(string id, string blockId, string text) => Change(id, s => { s.EditText(blockId, text); return 0; });
    public void MoveBlock(string id, string blockId, int delta) => Change(id, s => { s.MoveBlock(blockId, delta); return 0; });
    public void RemoveBlock(string id, string blockId) => Change(id, s => { s.RemoveBlock(blockId); return 0; });

    /// <summary>A new picture box, or (blockId given) a new picture in an existing box. Checks the package's room first.</summary>
    public async Task<Block> PictureAsync(string id, string slug, string? blockId, Stream content, string caption, CancellationToken ct)
    {
        var site = store.Get(id) ?? throw new SiteException("Siden findes ikke.");
        if (pictures.UsedBytes(id) >= SiteRules.Quota(site.Package))
            throw new SiteException("Der er ikke mere plads til billeder i din pakke. Skriv til Martin.");
        var stored = await pictures.SaveAsync(id, content, ct) ?? throw new SiteException("Det var ikke et billede.");
        return Change(id, s =>
        {
            if (string.IsNullOrEmpty(blockId)) return s.AddPicture(slug, stored, caption);
            s.ReplacePicture(blockId, stored);
            return s.BlockOf(blockId).Block;
        });
    }

    /// <summary>One step back. Returns false when there is nothing to undo.</summary>
    public bool Undo(string id)
    {
        lock (Gate)
        {
            if (store.PopHistory(id) is not { } previous) return false;
            previous.ChangedAt = clock.GetUtcNow().UtcDateTime;
            store.Save(previous);
            return true;
        }
    }

    public int UndoSteps(string id) => store.HistoryCount(id);

    private T Change<T>(string id, Func<Site, T> change)
    {
        lock (Gate)
        {
            var site = store.Get(id) ?? throw new SiteException("Siden findes ikke.");
            store.PushHistory(site);
            var result = change(site);
            site.ChangedAt = clock.GetUtcNow().UtcDateTime;
            store.Save(site);
            // Pictures neither the site nor any Fortryd copy uses any more are freed, so the package's room is honest.
            pictures.DeleteUnused(id, site.Pictures().ToHashSet());
            return result;
        }
    }
}
