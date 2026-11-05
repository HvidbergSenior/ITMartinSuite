using ITMartinSalg.Domain;

namespace ITMartinSalg.Application;

/// <summary>One line on Salg's Bud page: the thing, the highest hidden bid, and how many bid.</summary>
public sealed record BidRow(int ItemId, string Line, int TopKr, int Bidders, DateTime LastAt, bool ForSale);
/// <summary>One bundle on the Bud page: who, what (the lines), their bid or "spørger om pris", and its status.</summary>
public sealed record BundleRow(int Id, string Customer, IReadOnlyList<string> Lines, int? Kr, string Status, DateTime At);

/// <summary>
/// The marketplace's offers (user 2026-11-05: built on live auction + marketplace, and "make batches of items"): Byd = a hidden
/// maximum for the Saturday auction, Mit bundt = several things with one bid or a question. Only things that are for sale.
/// bogshoppen.dk checks the kundenummer in Kunder; this keeps the offers.
/// </summary>
public sealed class OfferUseCases(IOfferRepository offers, IStockGateway stock, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<(bool Ok, string? Error)> BidAsync(string? customer, int itemId, int maxKr, CancellationToken ct = default)
    {
        try
        {
            var bid = Bid.Place(customer, itemId, maxKr, Now);
            if (await stock.ItemAsync(itemId, ct) is null) return (false, "Varen er her ikke mere.");
            if (await offers.BidAsync(bid.Customer, itemId, ct) is { } was) { was.MaxKr = maxKr; was.PlacedAt = Now; bid = was; }
            await offers.SaveBidAsync(bid, ct);
            return (true, null);
        }
        catch (InvalidOperationException e) { return (false, e.Message); }
    }

    public async Task<Dictionary<int, int>> MyBidsAsync(string? customer, CancellationToken ct = default) =>
        Kundenummer.Normalise(customer) is { } code ? (await offers.MyBidsAsync(code, ct)).ToDictionary(b => b.ItemId, b => b.MaxKr) : [];

    public async Task<(bool Ok, string? Error)> BundleAsync(string? customer, IReadOnlyCollection<int> itemIds, int? kr, CancellationToken ct = default)
    {
        try
        {
            var bundle = BundleOffer.Make(customer, itemIds, kr, Now);
            var forSale = (await stock.ByIdsAsync(bundle.Ids.ToList(), ct)).Select(i => i.Id).ToHashSet();
            if (bundle.Ids.Any(id => !forSale.Contains(id))) return (false, "En af tingene er her ikke mere – tag den ud af bundtet.");
            await offers.AddBundleAsync(bundle, ct);
            return (true, null);
        }
        catch (InvalidOperationException e) { return (false, e.Message); }
    }

    /// <summary>Martin's Bud page: things with bids, highest first, and the bundles, newest first.</summary>
    public async Task<(List<BidRow> Bids, List<BundleRow> Bundles)> OverviewAsync(CancellationToken ct = default)
    {
        var bids = await offers.AllBidsAsync(ct);
        var bundles = await offers.BundlesAsync(ct);
        var ids = bids.Select(b => b.ItemId).Concat(bundles.SelectMany(b => b.Ids)).Distinct().ToList();
        var lines = ids.Count == 0 ? new Dictionary<int, string>() : (await stock.ByIdsAsync(ids, ct)).ToDictionary(i => i.Id, i => i.Line);
        string Line(int id) => lines.GetValueOrDefault(id) ?? $"Vare {id} (ikke til salg mere)";
        var bidRows = bids.GroupBy(b => b.ItemId)
            .Select(g => new BidRow(g.Key, Line(g.Key), g.Max(b => b.MaxKr), g.Count(), g.Max(b => b.PlacedAt), lines.ContainsKey(g.Key)))
            .OrderByDescending(r => r.TopKr).ToList();
        var bundleRows = bundles.OrderByDescending(b => b.CreatedAt)
            .Select(b => new BundleRow(b.Id, b.Customer, b.Ids.Select(Line).ToList(), b.Kr, b.Status, b.CreatedAt)).ToList();
        return (bidRows, bundleRows);
    }

    public async Task<bool> AnsweredAsync(int bundleId, CancellationToken ct = default)
    {
        if (await offers.BundleAsync(bundleId, ct) is not { } b) return false;
        b.MarkAnswered();
        await offers.SaveBundleAsync(b, ct);
        return true;
    }
}
