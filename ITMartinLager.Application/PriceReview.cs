using ITMartinLager.Domain;
using ITMartinVarer.Contracts;

namespace ITMartinLager.Application;

/// <summary>
/// What Vurdering asks Lager for (2026-11-05, step 2 as its own helper): the things that need a person's price, and
/// saving that price. Lager still owns the things and the price hunt; Vurdering only decides.
/// </summary>
public sealed class PriceReviewUseCases(ItemUseCases items, IBoxRepository boxRepo)
{
    public const int MaxFinds = 5;

    private string Valuation(string pile) => pile == VurderingPiles.NoPrice ? Valuations.NoPrice : Valuations.Assess;

    private async Task<List<Item>> PileAsync(string pile, CancellationToken ct) =>
        (await items.SearchAsync("", null, Valuation(pile), ct))?.Hits.Where(i => Statuses.Ours(i.Status) && i.Status != Statuses.Solgt).ToList() ?? [];

    /// <summary>The counts of both piles, and the next <paramref name="take"/> things of one (skipping the ones just passed).</summary>
    public async Task<VurderingQueue> QueueAsync(string pile, int take, IReadOnlyCollection<int> skip, CancellationToken ct = default)
    {
        var assess = await PileAsync(VurderingPiles.Assess, ct);
        var none = await PileAsync(VurderingPiles.NoPrice, ct);
        var next = (pile == VurderingPiles.NoPrice ? none : assess).Where(i => !skip.Contains(i.Id)).Take(Math.Clamp(take, 1, 10)).ToList();
        var boxes = new Dictionary<int, string>();
        foreach (var b in next.Select(i => i.BoxId).OfType<int>().Distinct())
            if (await boxRepo.GetAsync(b, ct) is { } box) boxes[b] = box.Code;
        var cards = new List<VurderingItem>();
        foreach (var i in next) cards.Add(await CardAsync(i, boxes, ct));
        return new VurderingQueue(assess.Count, none.Count, cards);
    }

    private async Task<VurderingItem> CardAsync(Item i, Dictionary<int, string> boxes, CancellationToken ct)
    {
        var finds = (await items.GetAsync(i.Id, ct))?.Finds ?? [];
        return new VurderingItem(i.Id, i.Line(), i.Kind, i.Condition, Kr(i.PriceHint), Kr(i.SimilarHint),
            i.BoxId is { } b && boxes.TryGetValue(b, out var code) ? code : "",
            finds.OrderBy(f => f.Kr).Take(MaxFinds).Select(f => new VurderingFind(f.Source, f.Name, f.Kr, f.Url, f.IsNew)).ToList());
    }

    private static int? Kr(decimal? d) => d is > 0 ? (int)Math.Round(d.Value) : null;

    /// <summary>The person's price. Only a thing that is ours and not sold; 1 to 100.000 kr.</summary>
    public async Task<string?> SetAsync(int id, int kr, CancellationToken ct = default)
    {
        if (kr is < 1 or > 100_000) return "Prisen skal være mellem 1 og 100.000 kr.";
        if (await items.GetAsync(id, ct) is not { } view) return "Varen findes ikke.";
        var item = view.Item;
        if (!Statuses.Ours(item.Status) || item.Status == Statuses.Solgt) return "Varen kan ikke prissættes – den er solgt eller ikke vores.";
        item.Price = kr;
        await items.SaveAsync(item, ct);
        return null;
    }
}
