using System.Globalization;
using System.Net.Http.Json;
using ITMartinIndkoeb.Application;
using ITMartinIndkoeb.Domain;
using Microsoft.Extensions.Configuration;

namespace ITMartinIndkoeb.Infrastructure;

/// <summary>The Tilbud app's own API (GET /api/tilbud, /api/sted) - Indkoeb__TilbudUrl, default the photoserver's tilbud-web.
/// Tilbud keeps its sources, ranking and app-price rules; Indkøb only shows the answer and puts it on the list.</summary>
internal sealed class TilbudKlient(HttpClient http, IConfiguration cfg) : ITilbudKilde
{
    private string Url => (cfg["Indkoeb:TilbudUrl"] ?? "http://10.0.0.200:9150").TrimEnd('/');
    private const int Km = 10, MaxTilbud = 80;   // hidden shops are taken out in Application, then 30 are shown

    public async Task<IReadOnlyList<TilbudVisning>> SoegAsync(string q, double lat, double lng, CancellationToken ct = default)
    {
        var url = $"{Url}/api/tilbud?q={Uri.EscapeDataString(q)}&lat={lat.ToString(CultureInfo.InvariantCulture)}&lng={lng.ToString(CultureInfo.InvariantCulture)}&km={Km}";
        try
        {
            var list = await http.GetFromJsonAsync<List<Offer>>(url, ct) ?? [];
            // Exact hits first (the list is already cheapest first within each), then the other products.
            return list.Where(o => o.Matches).OrderBy(o => o.Variant).Take(MaxTilbud).Select(o => new TilbudVisning(
                SeddelRegler.Gruppe(o.Chain), o.Title, o.Description, o.Price, o.NormalPrice,
                o.UnitPrice is { } u && o.UnitLabel is { } l ? $"{u:0.##} {l}" : null,
                o.ValidTo is { } t ? DateOnly.FromDateTime(t.ToLocalTime().DateTime) : null,
                o.Image, o.NeedsApp, o.NearestStore, o.NearestKm, o.Variant, o.AppName, o.AppPrice, o.NoAppPrice)).ToList();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            throw new TilbudIkkeNu();
        }
    }

    public async Task<Sted?> StedAsync(string postnr, CancellationToken ct = default)
    {
        try
        {
            var s = await http.GetFromJsonAsync<StedSvar>($"{Url}/api/sted?postnr={Uri.EscapeDataString(postnr)}", ct);
            return s is { Lat: not 0, Lng: not 0 } ? new Sted(postnr, s.Name ?? postnr, s.Lat, s.Lng) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed record Offer(string Chain, string Title, string? Description, decimal Price, decimal? NormalPrice, decimal? UnitPrice,
        string? UnitLabel, DateTimeOffset? ValidTo, string? Image, string? NeedsApp, string? NearestStore, double? NearestKm, bool Matches = true, bool Variant = false,
        string? AppName = null, decimal? AppPrice = null, decimal? NoAppPrice = null);
    private sealed record StedSvar(string? Name, double Lat, double Lng);
}
