using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITMartinLager.Server.Data;

namespace ITMartinLager.Server.Services;

// Barcode -> what it is, from free sources without keys: books by ISBN (Google Books, then Open Library), CDs by
// EAN (MusicBrainz), everything else (DVD, Blu-ray, games) by UPCitemdb's free trial (~100 a day), with kind and
// console guessed from the product title. A miss returns null - the person types it in.
public sealed class BarcodeLookup(HttpClient http, ILogger<BarcodeLookup> log)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private const string Ua = "BogshoppenLager/1.0 (ITMartin@Mensa.dk)";   // MusicBrainz requires a real contact

    public static string Clean(string code) => new(code.Where(char.IsDigit).ToArray());
    // Books before 2007 carry a 10-digit ISBN (last digit may be X); Google Books and the sites want the 13-digit one.
    public static string ToIsbn13(string typed)
    {
        var raw = new string(typed.Where(c => char.IsDigit(c) || c is 'X' or 'x').ToArray()).ToUpperInvariant();
        if (raw.Length != 10) return Clean(raw);
        var core = "978" + raw[..9];
        var sum = core.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return core + (10 - sum % 10) % 10;
    }

    public static bool IsIsbn(string code) => code.Length == 13 && (code.StartsWith("978") || code.StartsWith("979"));

    public async Task<Found?> LookupAsync(string code, CancellationToken ct = default)
    {
        code = Clean(code);
        if (code.Length < 8) return null;
        if (IsIsbn(code)) return await Try(() => GoogleBooks(code, ct), "Google Books") ?? await Try(() => OpenLibrary(code, ct), "Open Library");
        return await Try(() => MusicBrainz(code, ct), "MusicBrainz") ?? await Try(() => UpcItemDb(code, ct), "UPCitemdb");
    }

    private async Task<Found?> Try(Func<Task<Found?>> call, string source)
    {
        try { return await call(); }
        catch (Exception ex) { log.LogInformation("{Source} lookup failed: {Message}", source, ex.Message); return null; }
    }

    private async Task<T?> Get<T>(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd(Ua);
        req.Headers.Accept.ParseAdd("application/json");
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return default;
        return await res.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private static Found Book(string title, string author, string date, string code) =>
        new(Kinds.Bog, title.Trim(), author.Trim(), "", "", Year(date), "", "God", 1, 0.95, "", "", code);

    private async Task<Found?> GoogleBooks(string isbn, CancellationToken ct)
    {
        var r = await Get<GbResult>($"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}", ct);
        var v = r?.Items?.FirstOrDefault()?.VolumeInfo;
        return v?.Title is { Length: > 0 } t ? Book(t + (v.Subtitle is { Length: > 0 } s ? ": " + s : ""), string.Join(", ", v.Authors ?? []), v.PublishedDate ?? "", isbn) : null;
    }

    private async Task<Found?> OpenLibrary(string isbn, CancellationToken ct)
    {
        var r = await Get<Dictionary<string, OlBook>>($"https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data", ct);
        var b = r?.Values.FirstOrDefault();
        return b?.Title is { Length: > 0 } t ? Book(t, string.Join(", ", (b.Authors ?? []).Select(a => a.Name)), b.PublishDate ?? "", isbn) : null;
    }

    private async Task<Found?> MusicBrainz(string ean, CancellationToken ct)
    {
        var r = await Get<MbSearch>($"https://musicbrainz.org/ws/2/release/?query=barcode:{ean}&fmt=json&limit=1", ct);
        var rel = r?.Releases?.FirstOrDefault();
        if (rel is null || string.IsNullOrEmpty(rel.Title)) return null;
        var format = rel.Media?.FirstOrDefault()?.Format ?? "";
        var kind = format.Contains("DVD", StringComparison.OrdinalIgnoreCase) ? Kinds.Dvd
            : format.Contains("Blu", StringComparison.OrdinalIgnoreCase) ? Kinds.BluRay
            : format.Contains("CD", StringComparison.OrdinalIgnoreCase) ? Kinds.Cd : Kinds.Andet;
        var artist = string.Join("", (rel.ArtistCredit ?? []).Select(a => a.Name + (a.JoinPhrase ?? "")));
        return new Found(kind, rel.Title, artist, "", "", Year(rel.Date ?? ""), "", "God", 1, 0.95, format == "" ? "" : format, "", ean);
    }

    private async Task<Found?> UpcItemDb(string code, CancellationToken ct)
    {
        var r = await Get<UpcLookup>($"https://api.upcitemdb.com/prod/trial/lookup?upc={code}", ct);
        var title = r?.Items?.FirstOrDefault()?.Title;
        if (string.IsNullOrWhiteSpace(title)) return null;
        var (kind, platform) = Guess(title);
        return new Found(kind, title.Trim(), "", "", "", null, platform, "God", 1, 0.6, "", "", code);
    }

    // "Gran Turismo 3 A-Spec (PS2)" -> Konsolspil/PlayStation 2; "Matrix [Blu-ray]" -> Blu-ray.
    public static (string Kind, string Platform) Guess(string title)
    {
        // Punctuation becomes spaces, so "(PS2)", "[Blu-ray]" and "- Wii" all read as words.
        var t = " " + System.Text.RegularExpressions.Regex.Replace(title.ToLowerInvariant(), "[^a-z0-9æøå]+", " ") + " ";
        foreach (var (key, name) in Platforms)
            if (t.Contains(key)) return (Kinds.Konsolspil, name);
        if (t.Contains(" blu ray ") || t.Contains(" bluray ") || t.Contains(" 4k ")) return (Kinds.BluRay, "");
        if (t.Contains(" dvd ")) return (Kinds.Dvd, "");
        if (t.Contains(" cd ")) return (Kinds.Cd, "");
        return (Kinds.Andet, "");
    }

    private static readonly (string Key, string Name)[] Platforms =
    [
        ("playstation 5", "PlayStation 5"), (" ps5", "PlayStation 5"), ("playstation 4", "PlayStation 4"), (" ps4", "PlayStation 4"),
        ("playstation 3", "PlayStation 3"), (" ps3", "PlayStation 3"), ("playstation 2", "PlayStation 2"), (" ps2", "PlayStation 2"),
        ("playstation", "PlayStation"), (" psp", "PSP"), ("ps vita", "PS Vita"),
        ("xbox series", "Xbox Series"), ("xbox one", "Xbox One"), ("xbox 360", "Xbox 360"), ("xbox", "Xbox"),
        ("nintendo switch", "Nintendo Switch"), (" switch", "Nintendo Switch"), ("wii u", "Wii U"), (" wii", "Wii"),
        ("gamecube", "GameCube"), ("nintendo 64", "Nintendo 64"), (" n64", "Nintendo 64"), ("nintendo ds", "Nintendo DS"),
        ("3ds", "Nintendo 3DS"), ("game boy", "Game Boy"), ("gameboy", "Game Boy"), (" snes", "Super Nintendo"), (" nes ", "NES"),
        ("sega mega drive", "Sega Mega Drive"), ("dreamcast", "Dreamcast"),
    ];

    private static int? Year(string date) =>
        date.Length >= 4 && int.TryParse(date[..4], out var y) && y is > 1800 and < 2100 ? y : null;

    private sealed class GbResult { public List<GbItem>? Items { get; set; } }
    private sealed class GbItem { public GbVolume? VolumeInfo { get; set; } }
    private sealed class GbVolume { public string? Title { get; set; } public string? Subtitle { get; set; } public List<string>? Authors { get; set; } public string? PublishedDate { get; set; } }
    private sealed class OlBook { public string? Title { get; set; } public List<OlAuthor>? Authors { get; set; } [JsonPropertyName("publish_date")] public string? PublishDate { get; set; } }
    private sealed class OlAuthor { public string Name { get; set; } = ""; }
    private sealed class MbSearch { public List<MbRelease>? Releases { get; set; } }
    private sealed class MbRelease
    {
        public string? Title { get; set; }
        public string? Date { get; set; }
        [JsonPropertyName("artist-credit")] public List<MbCredit>? ArtistCredit { get; set; }
        public List<MbMedium>? Media { get; set; }
    }
    private sealed class MbCredit { public string Name { get; set; } = ""; public string? JoinPhrase { get; set; } }
    private sealed class MbMedium { public string? Format { get; set; } }
    private sealed class UpcLookup { public List<UpcItem>? Items { get; set; } }
    private sealed class UpcItem { public string? Title { get; set; } }
}
