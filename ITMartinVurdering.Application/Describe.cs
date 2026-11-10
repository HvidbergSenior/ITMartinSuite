using ITMartinVarer.Contracts;
using ITMartinVurdering.Domain;

namespace ITMartinVurdering.Application;

/// <summary>Tingshoppen/Strikkeshoppen descriptions (user 2026-11-10: "i want to have ai look on the things"): the dødsbo AI
/// looks at the waiting things' photos - up to DodsboRules.PerCall in ONE call, never more than one call per round, under its
/// daily cap - and Lager gets back a title, the story as the buyers' text, and a price range.</summary>
public sealed class DescribeUseCases(ILagerReview lager, IThingValuer valuer)
{
    /// <summary>One round: at most one AI call. Returns how many things were sent back to Lager.</summary>
    public async Task<int> RoundAsync(CancellationToken ct = default)
    {
        if (!valuer.Enabled || valuer.CallsLeftToday() <= 0) return 0;
        var waiting = await lager.WaitingDescriptionsAsync(DodsboRules.PerCall, ct);
        if (waiting is not { Count: > 0 }) return 0;
        var photos = new List<(VurderingDescribe Thing, ThingPhoto Photo)>();
        foreach (var t in waiting)
            if (await lager.PhotoAsync(t.Id, ct) is { Length: > 0 } jpeg)
                photos.Add((t, new ThingPhoto(photos.Count, jpeg, $"{t.Kind}: {t.Title}")));
        var answers = photos.Count == 0 ? [] : await valuer.ValueAsync(photos.Select(p => p.Photo).ToList(), ct);
        var sent = 0;
        foreach (var t in waiting)
        {
            var at = photos.FindIndex(p => p.Thing.Id == t.Id);
            var a = at < 0 ? null : answers.FirstOrDefault(x => x.Index == at);
            // No answer (or no photo) still goes back, so Lager does not ask about it again and again.
            var text = a is null ? "" : a.Story.Trim() != "" ? a.Story.Trim() : a.Reason.Trim();
            if (await lager.SetDescriptionAsync(t.Id, new VurderingDescription(a?.What ?? "", text, a?.Low, a?.High), ct)) sent++;
        }
        return sent;
    }
}
