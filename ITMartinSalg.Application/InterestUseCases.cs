using ITMartinSalg.Domain;

namespace ITMartinSalg.Application;

/// <summary>One row on Salg's Interesse page: a thing, how many marked it, and whether it is still for sale.</summary>
public sealed record InterestRow(int ItemId, string Line, string Kind, int Count, DateTime LastAt, bool ForSale, int? BuyNowKr);

/// <summary>"Interessant" on the marketplace (user 2026-10-24: customers "mark items interesting", NO buying yet, then a
/// live auction picked by the marks). The site checks the kundenummer in Kunder; this keeps the marks.</summary>
public sealed class InterestUseCases(IInterestRepository interests, IStockGateway stock, SalgOptions options, TimeProvider clock)
{
    /// <summary>Marks the thing for this customer, or takes the mark back when it is there. Only a thing that is for sale
    /// can be marked; taking a mark back always works.</summary>
    public async Task<ShopInterest> ToggleAsync(string? customer, int itemId, CancellationToken ct = default)
    {
        if (Kundenummer.Normalise(customer) is not { } code) return new(false, 0, "Log ind med dit kundenummer først.");
        var mark = await interests.FindAsync(code, itemId, ct);
        if (mark is not null)
            await interests.RemoveAsync(mark, ct);
        else
        {
            if (await stock.ItemAsync(itemId, ct) is null) return new(false, 0, "Varen er her ikke mere.");
            await interests.AddAsync(Interest.Mark(code, itemId, clock.GetUtcNow().UtcDateTime), ct);
        }
        var count = (await interests.CountsAsync([itemId], ct)).GetValueOrDefault(itemId);
        return new(mark is null, count);
    }

    public async Task<List<int>> MineAsync(string? customer, CancellationToken ct = default) =>
        Kundenummer.Normalise(customer) is { } code ? await interests.MineAsync(code, ct) : [];

    /// <summary>The most marked things first - what a live auction could start with.</summary>
    public async Task<List<InterestRow>> TopAsync(int max = 100, CancellationToken ct = default)
    {
        var top = await interests.TopAsync(max, ct);
        if (top.Count == 0) return [];
        var forSale = (await stock.ByIdsAsync(top.Select(t => t.ItemId).ToList(), ct)).ToDictionary(i => i.Id);
        return top.Select(t => forSale.GetValueOrDefault(t.ItemId) is { } i
                ? new InterestRow(t.ItemId, i.Line, i.Kind, t.Count, t.LastAt, true, Shop.BuyNowKr(i, options.ShopAutoMaxKr))
                : new InterestRow(t.ItemId, $"Vare {t.ItemId}", "", t.Count, t.LastAt, false, null))
            .ToList();
    }
}
