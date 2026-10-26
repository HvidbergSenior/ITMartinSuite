using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>What a blad costs new today - typed in on /kiosk, shown on the item page.</summary>
public sealed class KioskUseCases(IKioskRepository kiosk, TimeProvider clock)
{
    public Task<List<KioskPrice>> ListAsync(CancellationToken ct = default) => kiosk.ListAsync(ct);

    public async Task<KioskPrice?> ForAsync(Item item, CancellationToken ct = default) => Kiosk.For(item, await kiosk.ListAsync(ct));

    /// <summary>One price per series: the same name again updates it.</summary>
    public async Task SaveAsync(string series, int kr, string note, CancellationToken ct = default)
    {
        series = series.Trim();
        if (series == "") throw new LagerException("Skriv navnet på bladet.");
        if (kr is <= 0 or > 10000) throw new LagerException("Skriv prisen i hele kroner.");
        var same = (await kiosk.ListAsync(ct)).FirstOrDefault(p => string.Equals(p.Series, series, StringComparison.OrdinalIgnoreCase));
        var price = same ?? new KioskPrice { Series = series };
        (price.Kr, price.Note, price.UpdatedAt) = (kr, note.Trim(), clock.GetUtcNow().UtcDateTime);
        await kiosk.SaveAsync(price, ct);
    }

    public Task DeleteAsync(int id, CancellationToken ct = default) => kiosk.DeleteAsync(id, ct);
}
