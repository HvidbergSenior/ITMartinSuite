using ITMartinSalg.Domain;
using ITMartinVarer.Contracts;

namespace ITMartinSalg.Application;

public interface IVerdictRepository
{
    Task AddAsync(ItemVerdict v, CancellationToken ct);
    Task<ItemVerdict?> GetAsync(int id, CancellationToken ct);
    Task SaveAsync(ItemVerdict v, CancellationToken ct);
    Task<List<ItemVerdict>> ApprovedForAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct);
    Task<List<ItemVerdict>> NewAsync(CancellationToken ct);
    Task<int> NewOfAsync(string customer, CancellationToken ct);
}

/// <summary>"Hvad syntes du?" (2026-11-21): customers write, Martin approves on the Meninger page, bogshoppen.dk shows the approved.</summary>
public sealed class VerdictUseCases(IVerdictRepository verdicts, IStockGateway stock, TimeProvider clock)
{
    /// <summary>Waiting ones per customer - more is spam.</summary>
    public const int MaxWaitingPerCustomer = 10;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<ShopOfferAnswer> WriteAsync(int itemId, ShopVerdictPlace place, CancellationToken ct = default)
    {
        try
        {
            var v = ItemVerdict.Write(place.Customer, itemId, place.Name, place.Verdict, place.Teaser, Now);
            if (await stock.ItemAsync(itemId, ct) is null) return new(false, "Varen er her ikke mere.");
            if (await verdicts.NewOfAsync(v.Customer, ct) >= MaxWaitingPerCustomer) return new(false, "Vent, til vi har læst dine andre.");
            await verdicts.AddAsync(v, ct);
            return new(true);
        }
        catch (InvalidOperationException e) { return new(false, e.Message); }
    }

    public async Task<List<ShopVerdict>> ApprovedForAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct = default) =>
        itemIds.Count == 0 ? [] : (await verdicts.ApprovedForAsync(itemIds.Distinct().Take(100).ToList(), ct))
            .Select(v => new ShopVerdict(v.ItemId, v.Name, v.Verdict, v.Teaser)).ToList();

    /// <summary>Martin's page: the new ones with the thing's line.</summary>
    public async Task<List<(ItemVerdict Verdict, string Line)>> WaitingAsync(CancellationToken ct = default)
    {
        var list = await verdicts.NewAsync(ct);
        var ids = list.Select(v => v.ItemId).Distinct().ToList();
        var lines = ids.Count == 0 ? new Dictionary<int, string>() : (await stock.ByIdsAsync(ids, ct)).ToDictionary(i => i.Id, i => i.Line);
        return list.Select(v => (v, lines.GetValueOrDefault(v.ItemId) ?? $"Vare {v.ItemId}")).ToList();
    }

    public async Task<string?> DecideAsync(int id, bool approve, CancellationToken ct = default)
    {
        if (await verdicts.GetAsync(id, ct) is not { } v) return "Findes ikke.";
        try { if (approve) v.Approve(Now); else v.Reject(Now); await verdicts.SaveAsync(v, ct); return null; }
        catch (InvalidOperationException e) { return e.Message; }
    }
}
