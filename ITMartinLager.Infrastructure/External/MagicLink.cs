using System.Net.Http.Json;

namespace ITMartinLager.Infrastructure.External;

// The MTG Scanner (magicpriser, photoserver :9083) recognises Magic cards down to the exact printing and keeps the
// Scryfall price. Lager pulls one owner's cards into the register. Lager__MagicUrl + Lager__MagicKey (= Magic__LagerKey).
public sealed class MagicLink(HttpClient http, IConfiguration cfg) : IMagicGateway
{
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

    public Task<List<MagicOwner>> OwnersAsync(CancellationToken ct) => GetAsync<List<MagicOwner>>("/api/lager/owners", ct);

    public Task<List<MagicCardInfo>> CardsAsync(string owner, CancellationToken ct) =>
        GetAsync<List<MagicCardInfo>>("/api/lager/cards?owner=" + Uri.EscapeDataString(owner), ct);
}
