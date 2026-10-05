using System.Net.Http.Json;
using ITMartinLager.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Server.Services;

public sealed record MagicOwner(string Owner, int Cards, int Copies, decimal Eur);
public sealed record MagicCardInfo(Guid Id, string Name, string SetCode, string SetName, int? ReleaseYear, string CollectorNumber,
    int Quantity, decimal? EurPrice, decimal? UsdPrice, string? ScryfallId);

// The MTG Scanner (magicpriser, photoserver :9083) recognises Magic cards down to the exact printing and keeps the
// Scryfall price. Lager pulls one owner's cards into the register. Lager__MagicUrl + Lager__MagicKey (= Magic__LagerKey).
public sealed class MagicLink(HttpClient http, IConfiguration cfg)
{
    // The krone is pegged to the euro; Scryfall's EUR price is Cardmarket's trend price.
    public const decimal EurToDkk = 7.46m;

    private string Base => (cfg["Lager:MagicUrl"] ?? "http://10.0.0.200:9083").TrimEnd('/');
    public bool Enabled => (cfg["Lager:MagicKey"] ?? "").Length >= 16;

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Base + path);
        req.Headers.Add("X-Lager-Key", cfg["Lager:MagicKey"]);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"MTG Scanner svarede {(int)res.StatusCode}. Kører den? (Kontrol → magic-web)");
        return (await res.Content.ReadFromJsonAsync<T>(ct))!;
    }

    public Task<List<MagicOwner>> OwnersAsync(CancellationToken ct = default) => GetAsync<List<MagicOwner>>("/api/lager/owners", ct);

    public Task<List<MagicCardInfo>> CardsAsync(string owner, CancellationToken ct = default) =>
        GetAsync<List<MagicCardInfo>>("/api/lager/cards?owner=" + Uri.EscapeDataString(owner), ct);

    public static decimal? Kr(decimal? eur) => eur is { } e && e > 0 ? Math.Max(1, Math.Round(e * EurToDkk)) : null;

    // Fetching again updates the same rows (quantity, price hint) instead of adding duplicates. A price someone has
    // typed in Lager is never overwritten; a sold item is left alone.
    public static async Task<(int added, int updated)> ImportAsync(LagerDb db, int boxId, IEnumerable<MagicCardInfo> cards)
    {
        int added = 0, updated = 0;
        var refs = cards.Select(c => c.Id.ToString()).ToList();
        var existing = await db.Items.Where(i => refs.Contains(i.MagicRef)).ToDictionaryAsync(i => i.MagicRef);
        foreach (var c in cards)
        {
            var kr = Kr(c.EurPrice);
            var interest = $"Magic: The Gathering – {c.Name} fra {c.SetName}" + (c.ReleaseYear is { } y ? $" ({y})" : "") + ", nr. " + c.CollectorNumber
                + (kr is { } k ? $". Cardmarket-pris ca. {k:0} kr pr. kort (Scryfall)." : ".");
            if (existing.TryGetValue(c.Id.ToString(), out var item))
            {
                if (item.Status == Statuses.Solgt) continue;
                item.Quantity = Math.Max(1, c.Quantity);
                if (item.Interest == "" || item.Interest.StartsWith("Magic: The Gathering")) item.Interest = interest;
                item.PriceHint = kr;
                item.Touch();
                updated++;
                continue;
            }
            item = new Item
            {
                Kind = Kinds.Magic, Title = c.Name, Series = c.SetName, Number = c.CollectorNumber, Year = c.ReleaseYear,
                Quantity = Math.Max(1, c.Quantity), Condition = "God", BoxId = boxId, MagicRef = c.Id.ToString(),
                PriceHint = kr, Interest = interest, Confidence = 1,
            };
            item.Touch();
            db.Items.Add(item);
            added++;
        }
        await db.SaveChangesAsync();
        return (added, updated);
    }
}
