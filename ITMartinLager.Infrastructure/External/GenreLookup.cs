using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ITMartinLager.Infrastructure.External;

// Genre tags from free sources (2026-11-07, user: "pick sucategoies. like danktop, indie, pop"), no AI:
// - music: MusicBrainz release group (title + artist) tags, then the artist's tags + genres and country (DK -> GenreRules.DanishArtist).
//   No key; at most 1 call a second (MusicBrainz' rule), with a contact in the User-Agent.
// - film: TMDb search (Lager__TmdbKey, the same free key as Mine skiver) -> genre names; a season box searches tv.
// - books: Open Library search (title + author) -> subjects. No key.
// Null = the source did not answer (the job tries again next round); empty = nothing found (looked up, no genre).
public sealed partial class GenreLookup(HttpClient http, IConfiguration cfg, ILogger<GenreLookup> log) : IGenreSource
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private const string Ua = "BogshoppenLager/1.0 (ITMartin@Mensa.dk)";   // MusicBrainz requires a real contact
    private string TmdbKey => cfg["Lager:TmdbKey"] ?? "";

    public async Task<IReadOnlyList<string>?> TagsAsync(Item item, CancellationToken ct)
    {
        try
        {
            return item.Category switch
            {
                ProductTypes.Musik => await MusicAsync(item, ct),
                ProductTypes.Film => await FilmAsync(item, ct),
                ProductTypes.Boeger => await BookAsync(item, ct),
                _ => [],
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            log.LogInformation("Genre lookup for item {Id} failed: {Message}", item.Id, ex.Message);
            return null;
        }
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd(Ua);
        req.Headers.Accept.ParseAdd("application/json");
        using var res = await http.SendAsync(req, ct);
        if ((int)res.StatusCode is 429 or >= 500) throw new HttpRequestException($"{(int)res.StatusCode} from {req.RequestUri?.Host}");
        if (!res.IsSuccessStatusCode) return default;
        return await res.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private static string E(string s) => Uri.EscapeDataString(s);

    /// <summary>Lucene quoting for MusicBrainz: quotes and backslashes out.</summary>
    private static string Q(string s) => "\"" + s.Replace("\\", " ").Replace("\"", " ").Trim() + "\"";

    /// <summary>"(Remastered)", "[2CD]", "- Deluxe Edition" make a search miss.</summary>
    private static string Plain(string s) => Brackets().Replace(s, " ").Trim();

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]")]
    private static partial Regex Brackets();

    // ----- MusicBrainz -----

    private static readonly SemaphoreSlim MbGate = new(1, 1);
    private static DateTime _mbLast = DateTime.MinValue;

    private async Task<T?> MbAsync<T>(string path, CancellationToken ct)
    {
        await MbGate.WaitAsync(ct);
        try
        {
            var wait = _mbLast.AddMilliseconds(1100) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            try { return await GetAsync<T>("https://musicbrainz.org/ws/2/" + path, ct); }
            finally { _mbLast = DateTime.UtcNow; }
        }
        finally { MbGate.Release(); }
    }

    private async Task<IReadOnlyList<string>> MusicAsync(Item item, CancellationToken ct)
    {
        var title = Plain(item.Title);
        var artist = Plain(item.Artist);
        var tags = new List<string>();
        string? artistId = null;
        if (title != "")
        {
            var query = "releasegroup:" + Q(title) + (artist != "" ? " AND artist:" + Q(artist) : "");
            var found = await MbAsync<MbGroups>($"release-group/?query={E(query)}&limit=1&fmt=json", ct);
            if (found?.ReleaseGroups is [{ Score: >= 85 } g, ..])
            {
                tags.AddRange(Ordered(g.Tags));
                artistId = g.ArtistCredit is [{ Artist.Id: { Length: > 0 } id }, ..] ? id : null;
            }
        }
        MbArtist? a = null;
        if (artistId is not null)
            a = await MbAsync<MbArtist>($"artist/{artistId}?inc=tags+genres&fmt=json", ct);
        else if (artist != "" && !IsVarious(artist))
            a = (await MbAsync<MbArtists>($"artist/?query={E("artist:" + Q(artist))}&limit=1&fmt=json", ct))?.Artists is [{ Score: >= 90 } x, ..] ? x : null;
        if (a is not null)
        {
            tags.AddRange(Ordered((a.Genres ?? []).Concat(a.Tags ?? [])));
            if (a.Country == "DK") tags.Add(ITMartinLager.Domain.GenreRules.DanishArtist);
        }
        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsVarious(string artist) => artist.ToLowerInvariant() is "various" or "various artists" or "diverse" or "diverse kunstnere";

    private static IEnumerable<string> Ordered(IEnumerable<MbTag>? tags) =>
        (tags ?? []).Where(t => !string.IsNullOrWhiteSpace(t.Name)).OrderByDescending(t => t.Count).Select(t => t.Name!);

    private sealed record MbTag(string? Name, int Count);
    private sealed record MbCredit(MbArtistRef? Artist);
    private sealed record MbArtistRef(string? Id);
    private sealed record MbGroup(int Score, List<MbTag>? Tags, [property: JsonPropertyName("artist-credit")] List<MbCredit>? ArtistCredit);
    private sealed record MbGroups([property: JsonPropertyName("release-groups")] List<MbGroup>? ReleaseGroups);
    private sealed record MbArtist(int Score, string? Country, List<MbTag>? Tags, List<MbTag>? Genres);
    private sealed record MbArtists(List<MbArtist>? Artists);

    // ----- TMDb -----

    // TMDb's genre ids (movie + tv) -> English names GenreRules reads.
    private static readonly Dictionary<int, string> TmdbGenres = new()
    {
        [28] = "action", [12] = "adventure", [16] = "animation", [35] = "comedy", [80] = "crime", [99] = "documentary", [18] = "drama",
        [10751] = "family", [14] = "fantasy", [36] = "history", [27] = "horror", [10402] = "music", [9648] = "mystery", [10749] = "romance",
        [878] = "science fiction", [53] = "thriller", [10752] = "war", [37] = "western", [10759] = "action & adventure", [10762] = "kids",
        [10765] = "sci-fi & fantasy", [10766] = "soap", [10768] = "war & politics",
    };

    private async Task<IReadOnlyList<string>> FilmAsync(Item item, CancellationToken ct)
    {
        if (TmdbKey == "") return [];
        var title = Plain(Films.SeasonWordsOut(item.Title));
        if (title == "") return [];
        var kind = Films.IsSeason(item.Kind, item.Title) ? "tv" : "movie";
        var yearParam = kind == "tv" ? "first_air_date_year" : "year";
        TmdbPage? page = null;
        if (item.Year is { } y)
            page = await GetAsync<TmdbPage>($"https://api.themoviedb.org/3/search/{kind}?api_key={TmdbKey}&query={E(title)}&{yearParam}={y}&include_adult=false", ct);
        if (page?.Results is not { Count: > 0 })
            page = await GetAsync<TmdbPage>($"https://api.themoviedb.org/3/search/{kind}?api_key={TmdbKey}&query={E(title)}&include_adult=false", ct);
        if (page?.Results is not [var first, ..]) return [];
        return (first.GenreIds ?? []).Select(id => TmdbGenres.GetValueOrDefault(id)).OfType<string>().ToList();
    }

    private sealed record TmdbResult([property: JsonPropertyName("genre_ids")] List<int>? GenreIds);
    private sealed record TmdbPage(List<TmdbResult>? Results);

    // ----- Open Library -----

    private async Task<IReadOnlyList<string>> BookAsync(Item item, CancellationToken ct)
    {
        var title = Plain(item.Title);
        if (title == "") return [];
        var author = Plain(item.Artist);
        var url = $"https://openlibrary.org/search.json?title={E(title)}" + (author != "" ? $"&author={E(author)}" : "") + "&fields=subject&limit=3";
        var found = await GetAsync<OlPage>(url, ct);
        return (found?.Docs ?? []).SelectMany(d => d.Subject ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Take(60).ToList();
    }

    private sealed record OlDoc(List<string>? Subject);
    private sealed record OlPage(List<OlDoc>? Docs);
}
