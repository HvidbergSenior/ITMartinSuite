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
        (await items.SearchAsync("", null, Valuation(pile), ct))?.Hits.Where(i => Statuses.Ours(i.Status) && i.Status != Statuses.Solgt && Sure(i)).ToList() ?? [];

    /// <summary>Only things the reader was sure of come to Priser (user 2026-11-16: "pictures that look like this should not be
    /// found" - "Søs &amp; Kirsten", 0.3, "kun ryg delvist synlig"). Unsure ones are put right in Lager's "Ret de svære" first.
    /// A thing a person marked rare or priced always comes.</summary>
    public static bool Sure(Item i) => i.Rare || i.Price is > 0 || i.Confidence >= Pricing.MinConfidence;

    /// <summary>The counts of both piles, and the next <paramref name="take"/> things of one (skipping the ones just passed).</summary>
    public async Task<VurderingQueue> QueueAsync(string pile, int take, IReadOnlyCollection<int> skip, CancellationToken ct = default, string? usage = null)
    {
        var filter = VurderingFilter.Valid(usage);
        // The ones a person marked rare first (2026-11-16) - they are why the pile exists.
        var assess = (await PileAsync(VurderingPiles.Assess, ct)).Where(i => VurderingFilter.Shows(filter, i.Usage))
            .OrderByDescending(i => i.Rare).ToList();
        var none = (await PileAsync(VurderingPiles.NoPrice, ct)).Where(i => VurderingFilter.Shows(filter, i.Usage)).ToList();
        var next = (pile == VurderingPiles.NoPrice ? none : assess).Where(i => !skip.Contains(i.Id)).Take(Math.Clamp(take, 1, 10)).ToList();
        var boxes = new Dictionary<int, string>();
        foreach (var b in next.Select(i => i.BoxId).OfType<int>().Distinct())
            if (await boxRepo.GetAsync(b, ct) is { } box) boxes[b] = box.Code;
        var cards = new List<VurderingItem>();
        foreach (var i in next) cards.Add(await CardAsync(i, boxes, ct));
        return new VurderingQueue(assess.Count, none.Count, cards);
    }

    /// <summary>Find one thing to price (2026-11-16, user: "not easy to search for"): "3372" / "#3372" = that thing first, words =
    /// the matching things of ours, one at a time (skip = the ones passed). The pile counts stay as they are.</summary>
    public async Task<VurderingQueue> SearchAsync(string text, IReadOnlyCollection<int> skip, CancellationToken ct = default)
    {
        var t = (text ?? "").Trim().TrimStart('#');
        // A number may be the thing's own number (shown as "nr. 3372") or a year/issue ("1969") - the thing first, then the rest.
        var hits = new List<Item>();
        if (t != "" && int.TryParse(t, out var id) && await items.GetAsync(id, ct) is { } one) hits.Add(one.Item);
        if (t != "") hits.AddRange(((await items.SearchAsync(t, null, null, ct))?.Hits ?? []).Where(i => hits.All(h => h.Id != i.Id)));
        var ours = hits.Where(i => Statuses.Ours(i.Status) && i.Status != Statuses.Solgt).ToList();
        var next = ours.Where(i => Sure(i) && !skip.Contains(i.Id)).Take(1).ToList();
        var boxes = new Dictionary<int, string>();
        foreach (var b in next.Select(i => i.BoxId).OfType<int>().Distinct())
            if (await boxRepo.GetAsync(b, ct) is { } box) boxes[b] = box.Code;
        var cards = new List<VurderingItem>();
        foreach (var i in next) cards.Add(await CardAsync(i, boxes, ct));
        return new VurderingQueue((await PileAsync(VurderingPiles.Assess, ct)).Count, (await PileAsync(VurderingPiles.NoPrice, ct)).Count, cards,
            ours.Count(i => !Sure(i)));
    }

    private async Task<VurderingItem> CardAsync(Item i, Dictionary<int, string> boxes, CancellationToken ct)
    {
        var finds = ((await items.GetAsync(i.Id, ct))?.Finds ?? []).Where(f => Pricing.Shown(f.Source)).ToList();
        // The new price to compare with: the cheapest new copy at a Danish new shop (not a "magen til").
        var fresh = finds.Where(f => f.IsNew && !f.Similar && Pricing.DanishNewShops.Contains(f.Source) && f.Kr > 0).OrderBy(f => f.Kr).FirstOrDefault();
        var shown = finds.Where(f => !f.IsNew).OrderBy(f => f.Kr).Take(MaxFinds)
            .Concat(finds.Where(f => f.IsNew).OrderBy(f => f.Kr).Take(2)).ToList();
        // The market the suggestion came from (2026-11-15: "found on DBA 40 kr. Our price 25 pct -> 30 kr"): the same Danish-first middle as the hunt.
        var market = Pricing.NormalDanishFirst(finds.Where(f => !f.IsNew && !f.Similar && f.Kr > 0).Select(f => (f.Source, f.Kr)).ToList());
        return new VurderingItem(i.Id, i.Line(), i.Kind, i.Condition, Kr(i.PriceHint), Kr(i.SimilarHint),
            i.BoxId is { } b && boxes.TryGetValue(b, out var code) ? code : "",
            shown.Select(f => new VurderingFind(f.Source, f.Name, f.Kr, f.Url, f.IsNew, f.Similar)).ToList(),
            Usages.Normalise(i.Usage), fresh?.Kr, fresh?.Source, i.StickerKr, market, market is { } m ? Pricing.ShopRule(m) : "",
            i.Rare, i.Note, i.ItemPhoto != "", i.PhotoHidden, i.ItemPhoto != "" && await items.CoveredAsync(i.Id, ct), i.PhotoId is not null, i.PileBox);
    }

    public const int MaxNote = 1000, MaxMarkAtOnce = 200;

    /// <summary>Mark or unmark one thing rare. Rare = out of every deal at once (the 3 kr bin), waits for a person's price.
    /// Unmarked = back to the deal of its kind; the price hunter keeps it fresh from then on.</summary>
    public async Task<string?> SetRareAsync(int id, bool rare, CancellationToken ct = default)
    {
        if (await items.GetAsync(id, ct) is not { } view) return "Varen findes ikke.";
        var item = view.Item;
        if (!Statuses.Ours(item.Status) || item.Status == Statuses.Solgt) return "Varen er solgt eller ikke vores.";
        item.Rare = rare;
        item.Deal = Shop.DealFor(item, []);
        await items.SaveAsync(item, ct);
        return null;
    }

    /// <summary>Mark many rare at once. Returns how many were marked (sold and unknown ones are skipped).</summary>
    public async Task<int> MarkRareAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        var marked = 0;
        foreach (var id in ids.Distinct().Take(MaxMarkAtOnce))
            if (await SetRareAsync(id, true, ct) is null) marked++;
        return marked;
    }

    /// <summary>The person's comment (Item.Note), trimmed, at most MaxNote characters.</summary>
    public async Task<string?> SetNoteAsync(int id, string? note, CancellationToken ct = default)
    {
        var text = (note ?? "").Trim();
        if (text.Length > MaxNote) return $"Kommentaren må højst være {MaxNote} tegn.";
        if (await items.GetAsync(id, ct) is not { } view) return "Varen findes ikke.";
        view.Item.Note = text;
        await items.SaveAsync(view.Item, ct);
        return null;
    }

    private static int? Kr(decimal? d) => d is > 0 ? (int)Math.Round(d.Value) : null;

    /// <summary>"Som ny" or "Brugt" from Vurdering. "Ny" (never sold) is set in Lager only.</summary>
    public async Task<string?> SetUsageAsync(int id, string? usage, CancellationToken ct = default)
    {
        var u = Usages.Normalise(usage);
        if (u == Usages.New) return "Ny sættes i Lager.";
        if (await items.GetAsync(id, ct) is not { } view) return "Varen findes ikke.";
        if (!Statuses.Ours(view.Item.Status) || view.Item.Status == Statuses.Solgt) return "Varen er solgt eller ikke vores.";
        view.Item.Usage = u;
        await items.SaveAsync(view.Item, ct);
        return null;
    }

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
