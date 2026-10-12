using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Kolofons (2026-10-11, user: "to save in db and get price just cover. A job can run through kolofon if
/// needed"). Lager knows which books are worth one (price or first-edition look); Vareinfo takes and reads the photo
/// (2026-10-12 split) and sends what it read back here.</summary>
public sealed class KolofonUseCases(IItemRepository items, LagerOptions options, TimeProvider clock)
{
    public const string TakenInVareinfo = "vareinfo";

    public int FromKr => options.KolofonFromKr;

    public Task<List<(Item Item, int TopKr)>> WantedAsync(CancellationToken ct = default) => items.KolofonWantedAsync(FromKr, 200, ct);

    /// <summary>A photo was taken in Vareinfo: off the list, "venter på at blive læst" on the item page.</summary>
    public async Task<bool> TakenAsync(int itemId, CancellationToken ct = default)
    {
        if (await items.GetAsync(itemId, ct) is not { } item) return false;
        Kolofon.PhotoTaken(item, TakenInVareinfo);
        await items.UpdateAsync([item], ct);
        return true;
    }

    /// <summary>What Vareinfo read (null = unreadable) onto the book. A new ISBN makes the price hunter search again.</summary>
    public async Task<bool> ReadAsync(int itemId, KolofonReading? reading, CancellationToken ct = default)
    {
        if (await items.GetAsync(itemId, ct) is not { } item) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        if (item.KolofonPhoto == "") item.KolofonPhoto = TakenInVareinfo;
        if (reading is null) Kolofon.Unreadable(item, now);
        else Kolofon.Apply(item, reading, now);
        await items.UpdateAsync([item], ct);
        return true;
    }
}
