using ITMartinAdhd.Domain.Entities;

namespace ITMartinAdhd.Application;

// The household's things in the database.
public interface IItemStore
{
    Task<List<StoredItem>> RecentAsync(int count, CancellationToken ct = default);
    Task<List<StoredItem>> AllAsync(CancellationToken ct = default);
    Task<StoredItem?> GetAsync(int id, CancellationToken ct = default);
    Task AddAsync(StoredItem item, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task RemoveAsync(IEnumerable<StoredItem> items, CancellationToken ct = default);
}

// Photos of where things lie. Names are made by the store; only its own names are opened.
public interface IPhotoStore
{
    Task<string> SaveAsync(Stream photo, string extension, CancellationToken ct = default);
    string? PathOf(string? name);
    void Delete(string? name);
}

public sealed record ItemView(int Id, string Name, string Location, string? Notes, bool HasPhoto, DateTime StoredAt, DateTime UpdatedAt)
{
    public static ItemView Of(StoredItem i) => new(i.Id, i.Name, i.Location, i.Notes, i.PhotoPath != null, i.StoredAt, i.UpdatedAt);
}

// Everything the page can do: see the newest, search, put a thing down (with a photo), move it, delete it - and the
// daily clean-up of things nobody has touched for 30 days (photos go with them).
public sealed class Things(IItemStore store, IPhotoStore photos, TimeProvider clock)
{
    public const int RecentCount = 30, SearchMax = 20;
    private static readonly string[] PhotoTypes = [".jpg", ".jpeg", ".png", ".webp", ".heic"];

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<List<ItemView>> RecentAsync(CancellationToken ct = default) =>
        (await store.RecentAsync(RecentCount, ct)).Select(ItemView.Of).ToList();

    // Every word must match somewhere (thing, place or notes) - "nøgle bil" finds the car keys.
    public async Task<List<ItemView>> SearchAsync(string? query, CancellationToken ct = default)
    {
        var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return [];
        return (await store.AllAsync(ct))
            .Where(i => words.All(i.Matches))
            .OrderByDescending(i => i.UpdatedAt).Take(SearchMax).Select(ItemView.Of).ToList();
    }

    // null = name or place missing. A photo of an unknown type is ignored, not saved.
    public async Task<ItemView?> PutDownAsync(string? name, string? location, string? notes, Stream? photo, string? photoFileName, CancellationToken ct = default)
    {
        if (StoredItem.Create(name, location, notes, null, Now) is null) return null;
        string? file = null;
        var ext = Path.GetExtension(photoFileName ?? "").ToLowerInvariant();
        if (photo is not null && PhotoTypes.Contains(ext)) file = await photos.SaveAsync(photo, ext, ct);
        var item = StoredItem.Create(name, location, notes, file, Now)!;
        await store.AddAsync(item, ct);
        return ItemView.Of(item);
    }

    public async Task<bool> MoveAsync(int id, string? location, CancellationToken ct = default)
    {
        if (await store.GetAsync(id, ct) is not { } item || !item.MoveTo(location, Now)) return false;
        await store.SaveAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (await store.GetAsync(id, ct) is not { } item) return false;
        await store.RemoveAsync([item], ct);
        photos.Delete(item.PhotoPath);
        return true;
    }

    public async Task<string?> PhotoFileAsync(int id, CancellationToken ct = default) =>
        await store.GetAsync(id, ct) is { } item ? photos.PathOf(item.PhotoPath) : null;

    public async Task<int> ClearStaleAsync(CancellationToken ct = default)
    {
        var stale = (await store.AllAsync(ct)).Where(i => KeepRule.IsStale(i, Now)).ToList();
        if (stale.Count == 0) return 0;
        await store.RemoveAsync(stale, ct);
        foreach (var i in stale) photos.Delete(i.PhotoPath);
        return stale.Count;
    }
}
