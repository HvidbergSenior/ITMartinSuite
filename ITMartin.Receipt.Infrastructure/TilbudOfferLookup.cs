using System.Globalization;
using System.Net.Http.Json;
using ITMartin.Receipt.Application.Services;
using Microsoft.Extensions.Configuration;

namespace ITMartin.Receipt.Infrastructure;

/// <summary>The Tilbud app's own GET /api/tilbud (Receipt__TilbudUrl, default the photoserver's tilbud-web). Kvittering only
/// asks; Tilbud keeps its sources and rules.</summary>
public sealed class TilbudOfferLookup(HttpClient http, IConfiguration cfg) : IOfferLookup
{
    private string Url => (cfg["Receipt:TilbudUrl"] ?? "http://10.0.0.200:9150").TrimEnd('/');

    public async Task<IReadOnlyList<OfferInfo>> SearchAsync(string query, double lat, double lng, CancellationToken ct = default)
    {
        var url = $"{Url}/api/tilbud?q={Uri.EscapeDataString(query)}&lat={lat.ToString(CultureInfo.InvariantCulture)}&lng={lng.ToString(CultureInfo.InvariantCulture)}&km=15";
        using var res = await http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode) return [];   // "no offers" (400) is not an error here
        var list = await res.Content.ReadFromJsonAsync<List<Offer>>(cancellationToken: ct) ?? [];
        return list.Select(o => new OfferInfo(o.Chain, o.Title, o.Price, o.ValidFrom, o.ValidTo, o.NeedsApp, o.Condition)).ToList();
    }

    private sealed record Offer(string Chain, string Title, decimal Price, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, string? NeedsApp, string? Condition);
}
