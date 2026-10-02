using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace ITMartinMusicCheck.Server.Services;

// One disc in the visitor's collection. Kept in the browser (localStorage) only - see skiver.js.
public sealed class Disc
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "dvd";        // cd | dvd | bd
    public string Title { get; set; } = "";
    public string Sub { get; set; } = "";            // artist (CD) or year (film)
    public string Img { get; set; } = "";
    public string TmdbType { get; set; } = "";       // movie | tv
    public string TmdbId { get; set; } = "";
    public string Place { get; set; } = "";          // "Kasse 3", "Stuen, øverste hylde"
    // "Findes den digitalt?" (user 2026-10-02): stream | free | buy | none, "" = not checked yet. Who = the services, Checked = when.
    public string Digital { get; set; } = "";
    public string DigitalWho { get; set; } = "";
    public DateTime? Checked { get; set; }
    public DateTime Added { get; set; } = DateTime.UtcNow;
}

public sealed record Hit(string Title, string Sub, string Img, string TmdbType, string TmdbId);

public sealed record Provider(string Name, string Logo);

// Where a film can be watched in Denmark right now. Link = TMDB's page, which links on to JustWatch/the service.
public sealed record WatchInfo(string Link, List<Provider> Flatrate, List<Provider> Free, List<Provider> Rent, List<Provider> Buy)
{
    public bool Any => Flatrate.Count + Free.Count + Rent.Count + Buy.Count > 0;

    // The best way to get it digitally, for the badge + filter: something you may already pay for beats free beats buying.
    public (string Status, string Who) Best() =>
        Flatrate.Count > 0 ? ("stream", Names(Flatrate)) :
        Free.Count > 0 ? ("free", Names(Free)) :
        Buy.Count + Rent.Count > 0 ? ("buy", Names(Buy.Concat(Rent).DistinctBy(p => p.Name).ToList())) :
        ("none", "");

    private static string Names(List<Provider> l) => string.Join(", ", l.Take(4).Select(p => p.Name)) + (l.Count > 4 ? " …" : "");
}

// Free lookups only: TMDB (films/series + DK watch providers, key MineSkiver__TmdbKey) and MusicBrainz (CDs, no key).
public sealed class DiscLookup(IHttpClientFactory http, IConfiguration cfg, IMemoryCache cache, ILogger<DiscLookup> log)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private string Key => cfg["MineSkiver:TmdbKey"] ?? "";
    public bool HasTmdb => !string.IsNullOrWhiteSpace(Key);

    public async Task<List<Hit>> SearchAsync(string kind, string query, CancellationToken ct = default)
    {
        query = query.Trim();
        if (query.Length < 2) return [];
        try { return kind == "cd" ? await SearchCdAsync(query, ct) : await SearchFilmAsync(query, ct); }
        catch (Exception ex) { log.LogWarning("Search {Kind} '{Q}' failed: {Msg}", kind, query, ex.Message); return []; }
    }

    private async Task<List<Hit>> SearchFilmAsync(string query, CancellationToken ct)
    {
        if (!HasTmdb) return [];
        var url = $"https://api.themoviedb.org/3/search/multi?query={Uri.EscapeDataString(query)}&api_key={Key}&language=da-DK&include_adult=false";
        var res = await http.CreateClient("tmdb").GetFromJsonAsync<TmdbSearch>(url, Json, ct);
        return (res?.Results ?? [])
            .Where(r => r.MediaType is "movie" or "tv")
            .Take(8)
            .Select(r => new Hit(
                r.Title ?? r.Name ?? "",
                Year(r.ReleaseDate ?? r.FirstAirDate) + (r.MediaType == "tv" ? " · serie" : ""),
                r.PosterPath is null ? "" : $"https://image.tmdb.org/t/p/w185{r.PosterPath}",
                r.MediaType!, r.Id.ToString()))
            .ToList();
    }

    private async Task<List<Hit>> SearchCdAsync(string query, CancellationToken ct)
    {
        var q = Uri.EscapeDataString($"{query} AND primarytype:album");
        var res = await http.CreateClient("mb").GetFromJsonAsync<MbSearch>($"https://musicbrainz.org/ws/2/release-group?query={q}&fmt=json&limit=8", Json, ct);
        return (res?.ReleaseGroups ?? [])
            .Select(g => new Hit(
                g.Title ?? "",
                string.Join("", (g.ArtistCredit ?? []).Select(a => a.Name + a.JoinPhrase)) + (Year(g.FirstReleaseDate) is { Length: > 0 } y ? $" · {y}" : ""),
                $"https://coverartarchive.org/release-group/{g.Id}/front-250",
                "", ""))
            .ToList();
    }

    public async Task<WatchInfo?> WatchAsync(string tmdbType, string tmdbId, CancellationToken ct = default)
    {
        if (!HasTmdb || tmdbType is not ("movie" or "tv") || string.IsNullOrEmpty(tmdbId)) return null;
        var key = $"watch:{tmdbType}:{tmdbId}";
        if (cache.TryGetValue(key, out WatchInfo? hit)) return hit;
        try
        {
            var url = $"https://api.themoviedb.org/3/{tmdbType}/{tmdbId}/watch/providers?api_key={Key}";
            var res = await http.CreateClient("tmdb").GetFromJsonAsync<TmdbProviders>(url, Json, ct);
            var dk = res?.Results?.GetValueOrDefault("DK");
            var info = new WatchInfo(
                dk?.Link ?? $"https://www.themoviedb.org/{tmdbType}/{tmdbId}/watch?locale=DK",
                Map(dk?.Flatrate), Map((dk?.Free ?? []).Concat(dk?.Ads ?? []).ToList()), Map(dk?.Rent), Map(dk?.Buy));
            cache.Set(key, info, TimeSpan.FromHours(12));   // the catalogues change, but not by the minute
            return info;
        }
        catch (Exception ex) { log.LogWarning("Watch providers {T}/{Id} failed: {Msg}", tmdbType, tmdbId, ex.Message); return null; }
    }

    private static List<Provider> Map(List<TmdbProvider>? list) =>
        (list ?? []).Select(p => new Provider(p.ProviderName ?? "", p.LogoPath is null ? "" : $"https://image.tmdb.org/t/p/w92{p.LogoPath}")).ToList();

    private static string Year(string? date) => date is { Length: >= 4 } ? date[..4] : "";

    private sealed class TmdbSearch { public List<TmdbItem>? Results { get; set; } }
    private sealed class TmdbItem
    {
        public int Id { get; set; }
        [JsonPropertyName("media_type")] public string? MediaType { get; set; }
        public string? Title { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
        [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
    }
    private sealed class TmdbProviders { public Dictionary<string, TmdbCountry>? Results { get; set; } }
    private sealed class TmdbCountry
    {
        public string? Link { get; set; }
        public List<TmdbProvider>? Flatrate { get; set; }
        public List<TmdbProvider>? Free { get; set; }
        public List<TmdbProvider>? Ads { get; set; }
        public List<TmdbProvider>? Rent { get; set; }
        public List<TmdbProvider>? Buy { get; set; }
    }
    private sealed class TmdbProvider
    {
        [JsonPropertyName("provider_name")] public string? ProviderName { get; set; }
        [JsonPropertyName("logo_path")] public string? LogoPath { get; set; }
    }
    private sealed class MbSearch { [JsonPropertyName("release-groups")] public List<MbGroup>? ReleaseGroups { get; set; } }
    private sealed class MbGroup
    {
        public string Id { get; set; } = "";
        public string? Title { get; set; }
        [JsonPropertyName("first-release-date")] public string? FirstReleaseDate { get; set; }
        [JsonPropertyName("artist-credit")] public List<MbCredit>? ArtistCredit { get; set; }
    }
    private sealed class MbCredit
    {
        public string? Name { get; set; }
        [JsonPropertyName("joinphrase")] public string? JoinPhrase { get; set; }
    }
}
