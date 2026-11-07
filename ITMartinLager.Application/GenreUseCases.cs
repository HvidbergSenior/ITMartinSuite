using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Fills Item.Genre (user 2026-11-07: "pick sucategoies. like danktop, indie, pop", "as many genres for all the shops")
/// a few things a round, each looked up once (GenreAt). Music/film/books ask a free source; comics and games are read from their
/// own series and title (GenreRules.FromName - no call). No AI ("no cost on ai").</summary>
public sealed class GenreUseCases(IItemRepository items, IGenreSource source, TimeProvider clock)
{
    /// <summary>Hard cap per round: MusicBrainz allows 1 call a second, and a thing can take 2 calls.</summary>
    public const int MaxPerRound = 20;
    /// <summary>Things read from their own name cost nothing - more of them a round.</summary>
    public const int MaxByNamePerRound = 500;

    /// <returns>How many things got a genre (or were looked up without one).</returns>
    public async Task<int> FillAsync(int max, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var done = new List<Item>();
        // Comics and games: plain rules on the name.
        foreach (var item in await items.WithoutGenreAsync(Genres.Categories.Where(GenreRules.FromName).ToList(), MaxByNamePerRound, ct))
        {
            item.Genre = GenreRules.Pick(item.Category, [], item.Title, item.Series);
            item.GenreAt = now;
            done.Add(item);
        }
        // Music, film, books: a free source. One that does not answer (MusicBrainz "busy") waits; the others go on.
        // A few of each category a round, so 700 records do not keep the films and books waiting.
        var looked = Genres.Categories.Where(c => !GenreRules.FromName(c)).ToList();
        var each = Math.Max(1, Math.Clamp(max, 1, MaxPerRound) / looked.Count);
        foreach (var category in looked)
            foreach (var item in await items.WithoutGenreAsync([category], each, ct))
            {
                var tags = await source.TagsAsync(item, ct);
                if (tags is null) break;   // this source is busy or down - the next round tries again
                item.Genre = GenreRules.Pick(item.Category, tags, item.Title, item.Series);
                item.GenreAt = clock.GetUtcNow().UtcDateTime;
                done.Add(item);
            }
        if (done.Count > 0) await items.UpdateAsync(done, ct);
        return done.Count;
    }
}
