using ITMartinVarer.Contracts;

namespace ITMartinVurdering.Application;

/// <summary>Port: the gold list and the kiosk prices live in Lager's database; Vurdering shows and edits them (2026-11-15).</summary>
public interface ILagerPriceLists
{
    /// <summary>Null = Lager did not answer.</summary>
    Task<List<VurderingGold>?> GoldAsync(CancellationToken ct);
    Task<List<VurderingKiosk>?> KioskAsync(CancellationToken ct);
    /// <returns>Null when saved, else Lager's (Danish) reason.</returns>
    Task<string?> SaveKioskAsync(VurderingKioskSave price, CancellationToken ct);
    Task<bool> DeleteKioskAsync(int id, CancellationToken ct);
}

/// <summary>Guld, sølv og bronze + Ny i kiosken (user 2026-11-15: "pricing should be in pricing" - moved from Lager).</summary>
public sealed class PriceListUseCases(ILagerPriceLists lager)
{
    public Task<List<VurderingGold>?> GoldAsync(CancellationToken ct = default) => lager.GoldAsync(ct);
    public Task<List<VurderingKiosk>?> KioskAsync(CancellationToken ct = default) => lager.KioskAsync(ct);

    public async Task<ReviewResult> SaveKioskAsync(string? series, string? kr, string? note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(series)) return new(false, "Skriv navnet på bladet.");
        if (!int.TryParse((kr ?? "").Trim().Replace(" kr", ""), out var n) || n is <= 0 or > 10000) return new(false, "Skriv prisen i hele kroner.");
        return await lager.SaveKioskAsync(new(series.Trim(), n, (note ?? "").Trim()), ct) is { } error ? new(false, error) : new(true, $"Gemt: {series.Trim()} {n} kr.");
    }

    public async Task<ReviewResult> DeleteKioskAsync(int id, CancellationToken ct = default) =>
        await lager.DeleteKioskAsync(id, ct) ? new(true, "Slettet.") : new(false, "Lager svarer ikke lige nu – prøv igen om lidt.");
}
