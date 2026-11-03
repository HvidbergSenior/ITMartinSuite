using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Fund (user 2026-11-03): things photographed where they are for sale (a Fund session in Vareinfo) are priced like
/// stock but are not ours. Martin picks: "Tog den" = it becomes stock; "Færdig" = the rest is Ikke købt (kept, hidden).
/// No AI here - the reading and prices are the ones Lager makes anyway.</summary>
public sealed class FundUseCases(IBoxRepository boxes, IItemRepository items, IGoldRepository gold)
{
    /// <summary>Every Fund session's box with its counts, newest first.</summary>
    public async Task<List<FundTrip>> TripsAsync(CancellationToken ct = default)
    {
        var list = new List<FundTrip>();
        foreach (var box in await boxes.FundBoxesAsync(ct))
            list.Add(FundRules.Trip(box, await items.InBoxAsync(box.Id, ct)));
        return list;
    }

    /// <summary>One trip: the things still to decide (interesting and dearest first), and the ones taken home.</summary>
    public async Task<(Box Box, List<FundRow> Open, List<FundRow> Taken, int NotBought)?> TripAsync(int boxId, CancellationToken ct = default)
    {
        if (await boxes.GetAsync(boxId, ct) is not { Fund: true } box) return null;
        var all = await items.InBoxAsync(boxId, ct);
        var g = await gold.AllAsync(ct);
        return (box, FundRules.Rows(all.Where(i => i.Status == Statuses.Fund), g), FundRules.Rows(all.Where(i => Statuses.Ours(i.Status)), g),
            all.Count(i => i.Status == Statuses.IkkeKoebt));
    }

    /// <summary>"Tog den": it is ours now - in stock, so it goes on the page and into the deals like the rest.</summary>
    public Task TakeAsync(int itemId, CancellationToken ct = default) => ChangeAsync(itemId, Statuses.Fund, Statuses.Lager, ct);

    /// <summary>"Fortryd": back on the Fund list.</summary>
    public Task UndoAsync(int itemId, CancellationToken ct = default) => ChangeAsync(itemId, Statuses.Lager, Statuses.Fund, ct);

    /// <summary>"Færdig": the rest was not bought. Returns how many.</summary>
    public Task<int> FinishAsync(int boxId, CancellationToken ct = default) => MoveAllAsync(boxId, Statuses.Fund, Statuses.IkkeKoebt, ct);

    /// <summary>"Åbn igen": the not-bought ones back on the list (a mistake, or back at the same place).</summary>
    public Task<int> ReopenAsync(int boxId, CancellationToken ct = default) => MoveAllAsync(boxId, Statuses.IkkeKoebt, Statuses.Fund, ct);

    private async Task ChangeAsync(int itemId, string from, string to, CancellationToken ct)
    {
        if (await items.GetAsync(itemId, ct) is not { } item || item.Status != from || item.Box is not { Fund: true }) return;
        item.Status = to;
        item.Touch();
        await items.UpdateAsync([item], ct);
    }

    private async Task<int> MoveAllAsync(int boxId, string from, string to, CancellationToken ct)
    {
        if (await boxes.GetAsync(boxId, ct) is not { Fund: true }) return 0;
        var moved = (await items.InBoxAsync(boxId, ct)).Where(i => i.Status == from).ToList();
        foreach (var i in moved) { i.Status = to; i.Touch(); }
        if (moved.Count > 0) await items.UpdateAsync(moved, ct);
        return moved.Count;
    }
}
