using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Fills Item.Genre (user 2026-11-07: "pick sucategoies. like danktop, indie, pop") from free sources, a few things a
/// round, each looked up once (GenreAt). No AI ("no cost on ai"). The rules that turn tags into a genre are GenreRules.</summary>
public sealed class GenreUseCases(IItemRepository items, IGenreSource source, TimeProvider clock)
{
    /// <summary>Hard cap per round: MusicBrainz allows 1 call a second, and a thing can take 2 calls.</summary>
    public const int MaxPerRound = 20;

    /// <returns>How many things got looked up.</returns>
    public async Task<int> FillAsync(int max, CancellationToken ct = default)
    {
        var waiting = await items.WithoutGenreAsync(Genres.Categories, Math.Clamp(max, 1, MaxPerRound), ct);
        var done = new List<Item>();
        foreach (var item in waiting)
        {
            var tags = await source.TagsAsync(item, ct);
            if (tags is null) break;   // the source is down - the rest waits for the next round
            item.Genre = GenreRules.Pick(item.Category, tags, item.Title);
            item.GenreAt = clock.GetUtcNow().UtcDateTime;
            done.Add(item);
        }
        if (done.Count > 0) await items.UpdateAsync(done, ct);
        return done.Count;
    }
}
