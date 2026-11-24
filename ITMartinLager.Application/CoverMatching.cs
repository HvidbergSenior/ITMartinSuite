using ITMartinLager.Domain;

namespace ITMartinLager.Application;

// Compares our front cover with each used listing's picture (CoverMatch). A run takes at most `max` pictures, fetched one at a time
// with a pause, so the listing sites are never hammered. Free: no AI, only OpenCV on the server.
public sealed class CoverMatchUseCases(IItemRepository items, IPriceRepository prices, IPhotoStore photos, ICoverMatcher matcher)
{
    public sealed record Run(int Compared, int SameCover, int OtherEdition, int NotFetched, int Left);

    public async Task<Run> RunAsync(int max = 200, TimeSpan? pause = null, CancellationToken ct = default)
    {
        var covers = (await items.AllListedAsync(ct)).Where(i => i.ItemPhoto != "" && CoverMatch.CoverShaped(i.PhotoW, i.PhotoH))
            .ToDictionary(i => i.Id);
        var todo = await prices.FindsWithoutCoverAsync(covers.Keys.ToList(), int.MaxValue, ct);
        int compared = 0, same = 0, other = 0, failed = 0;
        var mine = new Dictionary<int, byte[]?>();
        foreach (var f in todo.Take(max))
        {
            if (!mine.TryGetValue(f.ItemId, out var ours)) mine[f.ItemId] = ours = await photos.ReadAsync(covers[f.ItemId].ItemPhoto, ct);
            if (ours is null) continue;
            var points = await matcher.MatchAsync(ours, f.Image, ct);
            if (points is null) { failed++; await prices.SetCoverAsync(f.Id, -1, ct); }   // -1 = tried, no picture: never again this run
            else
            {
                compared++;
                if (points >= CoverMatch.SameFrom) same++; else other++;
                await prices.SetCoverAsync(f.Id, points.Value, ct);
            }
            await Task.Delay(pause ?? TimeSpan.FromMilliseconds(400), ct);
        }
        return new Run(compared, same, other, failed, Math.Max(0, todo.Count - max));
    }
}
