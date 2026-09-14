using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITMartinBibliotek.Server.Data.Entities;

namespace ITMartinBibliotek.Server.Services;

public sealed record Candidate(
    string Source, string Title, string Artist, int? Year, string CoverUrl,
    MediaKind Kind, MediaFormat? Format,
    string TmdbId = "", string MusicBrainzId = "", string DiscogsId = "");

// Barcode and title lookups against the free metadata sources named in the
// brief. Every call is one HTTP request per user action (scan or search),
// never a loop over the catalog.
public sealed class MetadataLookup(IHttpClientFactory httpFactory, SettingsStore settings, ILogger<MetadataLookup> log)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // Order matters: MusicBrainz needs no key and answers most CDs; Discogs
    // covers what MusicBrainz misses; a generic product database turns a DVD
    // barcode into a title we can then search on TMDB.
    public async Task<List<Candidate>> ByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        barcode = new string(barcode.Where(char.IsDigit).ToArray());
        var found = new List<Candidate>();
        if (barcode.Length < 8) return found;

        found.AddRange(await Safe(() => MusicBrainzByBarcodeAsync(barcode, ct), "MusicBrainz"));
        found.AddRange(await Safe(() => DiscogsByBarcodeAsync(barcode, ct), "Discogs"));

        if (found.Count == 0)
        {
            var productName = await Safe(() => UpcItemDbAsync(barcode, ct), "UPCitemdb");
            if (productName is not null)
            {
                var query = TitleMatch.CleanProductName(productName);
                var format = GuessFormat(productName);
                var hits = await Safe(() => SearchTmdbAsync(query, ct), "TMDB");
                if (hits.Count == 0)
                    hits.Add(new Candidate("Stregkode", query, "", TitleMatch.YearIn(productName), "", MediaKind.Film, format));
                found.AddRange(hits.Select(h => h with { Format = format ?? h.Format }));
            }
        }
        return found;
    }

    public Task<List<Candidate>> SearchTitleAsync(string query, MediaKind kind, CancellationToken ct = default) =>
        kind == MediaKind.Musik
            ? Safe(() => MusicBrainzByTitleAsync(query, ct), "MusicBrainz")
            : Safe(() => SearchTmdbAsync(query, ct), "TMDB");

    // ---- MusicBrainz + Cover Art Archive ----

    private async Task<List<Candidate>> MusicBrainzByBarcodeAsync(string barcode, CancellationToken ct) =>
        await MusicBrainzAsync($"barcode:{barcode}", ct);

    private async Task<List<Candidate>> MusicBrainzByTitleAsync(string title, CancellationToken ct) =>
        await MusicBrainzAsync($"release:\"{title.Replace("\"", "")}\"", ct);

    private async Task<List<Candidate>> MusicBrainzAsync(string query, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("musicbrainz");
        var url = $"https://musicbrainz.org/ws/2/release/?query={Uri.EscapeDataString(query)}&fmt=json&limit=8";
        var res = await http.GetFromJsonAsync<MbSearch>(url, Json, ct);
        return (res?.Releases ?? [])
            .Select(r => new Candidate(
                "MusicBrainz", r.Title,
                string.Join(", ", r.ArtistCredit.Select(a => a.Name)),
                TitleMatch.YearIn(r.Date ?? ""),
                $"https://coverartarchive.org/release/{r.Id}/front-250",
                MediaKind.Musik, MediaFormat.Cd, MusicBrainzId: r.Id))
            .ToList();
    }

    // ---- Discogs (needs a personal token; skipped silently without one) ----

    private async Task<List<Candidate>> DiscogsByBarcodeAsync(string barcode, CancellationToken ct)
    {
        var token = await settings.GetAsync(SettingsStore.DiscogsToken);
        if (string.IsNullOrWhiteSpace(token)) return [];
        var http = httpFactory.CreateClient("discogs");
        var url = $"https://api.discogs.com/database/search?barcode={barcode}&token={token}&per_page=8";
        var res = await http.GetFromJsonAsync<DiscogsSearch>(url, Json, ct);
        return (res?.Results ?? []).Select(r =>
        {
            var parts = r.Title.Split(" - ", 2);
            var isVideo = r.Format.Any(f => f.Contains("DVD", StringComparison.OrdinalIgnoreCase) || f.Contains("Blu", StringComparison.OrdinalIgnoreCase));
            return new Candidate(
                "Discogs",
                parts.Length == 2 ? parts[1] : r.Title,
                parts.Length == 2 ? parts[0] : "",
                int.TryParse(r.Year, out var y) ? y : null,
                r.CoverImage ?? "",
                isVideo ? MediaKind.Film : MediaKind.Musik,
                GuessFormat(string.Join(" ", r.Format)) ?? MediaFormat.Cd,
                DiscogsId: r.Id.ToString());
        }).ToList();
    }

    // ---- UPCitemdb trial (no key, ~100 lookups/day) ----

    private async Task<string?> UpcItemDbAsync(string barcode, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("upc");
        var res = await http.GetFromJsonAsync<UpcLookup>($"https://api.upcitemdb.com/prod/trial/lookup?upc={barcode}", Json, ct);
        return res?.Items.FirstOrDefault()?.Title;
    }

    // ---- TMDB ----

    private async Task<List<Candidate>> SearchTmdbAsync(string query, CancellationToken ct)
    {
        var key = await settings.GetAsync(SettingsStore.TmdbApiKey);
        if (string.IsNullOrWhiteSpace(key)) return [];
        var http = httpFactory.CreateClient("tmdb");
        var url = $"https://api.themoviedb.org/3/search/multi?query={Uri.EscapeDataString(query)}&api_key={key}&language=da-DK&include_adult=false";
        var res = await http.GetFromJsonAsync<TmdbSearch>(url, Json, ct);
        return (res?.Results ?? [])
            .Where(r => r.MediaType is "movie" or "tv")
            .Take(8)
            .Select(r => new Candidate(
                "TMDB",
                r.Title ?? r.Name ?? "",
                "",
                TitleMatch.YearIn(r.ReleaseDate ?? r.FirstAirDate ?? ""),
                r.PosterPath is null ? "" : $"https://image.tmdb.org/t/p/w342{r.PosterPath}",
                r.MediaType == "tv" ? MediaKind.Serie : MediaKind.Film,
                null,
                TmdbId: r.Id.ToString()))
            .ToList();
    }

    public static MediaFormat? GuessFormat(string text)
    {
        if (text.Contains("blu", StringComparison.OrdinalIgnoreCase) || text.Contains("4k", StringComparison.OrdinalIgnoreCase)) return MediaFormat.BluRay;
        if (text.Contains("dvd", StringComparison.OrdinalIgnoreCase)) return MediaFormat.Dvd;
        if (text.Contains("cd", StringComparison.OrdinalIgnoreCase)) return MediaFormat.Cd;
        return null;
    }

    private async Task<T> Safe<T>(Func<Task<T>> call, string source) where T : new()
    {
        try { return await call(); }
        catch (Exception ex)
        {
            log.LogWarning("{Source} lookup failed: {Message}", source, ex.Message);
            return new T();
        }
    }

    private async Task<string?> Safe(Func<Task<string?>> call, string source)
    {
        try { return await call(); }
        catch (Exception ex)
        {
            log.LogWarning("{Source} lookup failed: {Message}", source, ex.Message);
            return null;
        }
    }

    private sealed class MbSearch { public List<MbRelease> Releases { get; set; } = []; }
    private sealed class MbRelease
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Date { get; set; }
        [JsonPropertyName("artist-credit")] public List<MbArtist> ArtistCredit { get; set; } = [];
    }
    private sealed class MbArtist { public string Name { get; set; } = ""; }

    private sealed class DiscogsSearch { public List<DiscogsResult> Results { get; set; } = []; }
    private sealed class DiscogsResult
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public string? Year { get; set; }
        [JsonPropertyName("cover_image")] public string? CoverImage { get; set; }
        public List<string> Format { get; set; } = [];
    }

    private sealed class UpcLookup { public List<UpcItem> Items { get; set; } = []; }
    private sealed class UpcItem { public string Title { get; set; } = ""; }

    private sealed class TmdbSearch { public List<TmdbResult> Results { get; set; } = []; }
    private sealed class TmdbResult
    {
        public long Id { get; set; }
        [JsonPropertyName("media_type")] public string MediaType { get; set; } = "";
        public string? Title { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
        [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
    }
}
