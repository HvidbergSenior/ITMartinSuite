using System.Text.Json;
using System.Text.Json.Nodes;
using ITMartinFaellesskab.Application;
using ITMartinFaellesskab.Domain;
using ITMartinKunder.Client;
using ITMartinKunder.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinFaellesskab.Infrastructure;

/// <summary>
/// Messages, members and "right now" in three small JSON files in the data folder (Faellesskab:DataDir). Written via a
/// temp file, so a crash never leaves half a file.
/// </summary>
public sealed class JsonCommunityStore : ICommunityStore
{
    private readonly string _msgFile, _memberFile, _nowFile;

    public JsonCommunityStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _msgFile = Path.Combine(dataDir, "messages.json");
        _memberFile = Path.Combine(dataDir, "members.json");
        _nowFile = Path.Combine(dataDir, "now.json");
    }

    public List<Message> LoadMessages() => Load<List<Message>>(_msgFile) ?? [];
    public void SaveMessages(IReadOnlyList<Message> messages) => Save(_msgFile, messages);
    public List<Member> LoadMembers() => Load<List<Member>>(_memberFile) ?? [];
    public void SaveMembers(IReadOnlyList<Member> members) => Save(_memberFile, members);
    public List<NowItem> LoadNow() => Load<List<NowItem>>(_nowFile) ?? [];
    public void SaveNow(IReadOnlyList<NowItem> now) => Save(_nowFile, now);

    private static T? Load<T>(string file)
    {
        if (!File.Exists(file)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(file)); } catch { return default; }
    }

    private static void Save<T>(string file, T data)
    {
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data));
        File.Move(tmp, file, true);
    }
}

/// <summary>The kundenummer in Kunder - every customer whose number works (KunderRoles.Market, the same as bogshoppen.dk).</summary>
public sealed class KunderCustomerCheck(KunderClient kunder) : ICustomerCheck
{
    // KunderClient answers with this text when Kunder could not be reached (it never throws).
    private const string NotReached = "Vi kan ikke tjekke";

    public async Task<(bool Ok, bool Reached, string Name, string Error)> CheckAsync(string code, CancellationToken ct = default)
    {
        var r = await kunder.CheckCodeAsync(code, KunderRoles.Market, ct);
        return (r.Ok, r.Ok || !r.Error.StartsWith(NotReached, StringComparison.Ordinal), r.Name, r.Error);
    }
}

// ---- the free tip APIs (no keys). Each returns at most a handful; Tips keeps the answer 6 hours. ----

/// <summary>Books: openlibrary.org search.</summary>
public sealed class OpenLibraryTips(HttpClient http) : ITipSource
{
    public TipKind Kind => TipKind.Books;

    public async Task<List<Tip>> SearchAsync(string query, CancellationToken ct = default)
    {
        var j = await http.GetFromJsonNode($"https://openlibrary.org/search.json?q={Uri.EscapeDataString(query)}&limit=8&fields=key,title,author_name,first_publish_year,cover_i", ct);
        return (j?["docs"]?.AsArray() ?? []).Select(d => new Tip(
            S(d?["title"]),
            string.Join(", ", (d?["author_name"]?.AsArray() ?? []).Take(2).Select(S)) + (d?["first_publish_year"] is { } y ? $" · {y}" : ""),
            "https://openlibrary.org" + S(d?["key"]),
            d?["cover_i"] is { } c ? $"https://covers.openlibrary.org/b/id/{c}-M.jpg" : "")).Where(t => t.Title.Length > 0).ToList();
    }

    internal static string S(JsonNode? n) => n?.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : n?.ToString() ?? "";
}

/// <summary>Magic: The Gathering: api.scryfall.com card search (asks for a User-Agent and Accept header).</summary>
public sealed class ScryfallTips(HttpClient http) : ITipSource
{
    public TipKind Kind => TipKind.Magic;

    public async Task<List<Tip>> SearchAsync(string query, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"https://api.scryfall.com/cards/search?q={Uri.EscapeDataString(query)}&order=edhrec&unique=cards", ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return [];   // Scryfall: nothing matched
        res.EnsureSuccessStatusCode();
        var j = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct));
        return (j?["data"]?.AsArray() ?? []).Take(8).Select(d => new Tip(
            OpenLibraryTips.S(d?["name"]),
            OpenLibraryTips.S(d?["type_line"]) + (d?["set_name"] is { } s ? " · " + OpenLibraryTips.S(s) : ""),
            OpenLibraryTips.S(d?["scryfall_uri"]),
            OpenLibraryTips.S(d?["image_uris"]?["small"] ?? d?["card_faces"]?[0]?["image_uris"]?["small"]))).Where(t => t.Title.Length > 0).ToList();
    }
}

/// <summary>Music: musicbrainz.org release groups (albums), cover from coverartarchive.org (asks for a User-Agent).</summary>
public sealed class MusicBrainzTips(HttpClient http) : ITipSource
{
    public TipKind Kind => TipKind.Music;

    public async Task<List<Tip>> SearchAsync(string query, CancellationToken ct = default)
    {
        var j = await http.GetFromJsonNode($"https://musicbrainz.org/ws/2/release-group?query={Uri.EscapeDataString(query)}&type=album&fmt=json&limit=8", ct);
        return (j?["release-groups"]?.AsArray() ?? []).Select(d =>
        {
            var id = OpenLibraryTips.S(d?["id"]);
            var artist = OpenLibraryTips.S(d?["artist-credit"]?[0]?["name"]);
            var date = OpenLibraryTips.S(d?["first-release-date"]);
            return new Tip(OpenLibraryTips.S(d?["title"]), artist + (date.Length >= 4 ? " · " + date[..4] : ""),
                "https://musicbrainz.org/release-group/" + id, id.Length > 0 ? $"https://coverartarchive.org/release-group/{id}/front-250" : "");
        }).Where(t => t.Title.Length > 0).ToList();
    }
}

/// <summary>PC games: the Steam store search (Danish prices).</summary>
public sealed class SteamTips(HttpClient http) : ITipSource
{
    private static readonly System.Globalization.CultureInfo Da = new("da-DK");
    public TipKind Kind => TipKind.PcGames;

    public async Task<List<Tip>> SearchAsync(string query, CancellationToken ct = default)
    {
        var j = await http.GetFromJsonNode($"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(query)}&l=danish&cc=DK", ct);
        return (j?["items"]?.AsArray() ?? []).Take(8).Select(d => new Tip(
            OpenLibraryTips.S(d?["name"]),
            d?["price"]?["final"] is { } p && p.GetValueKind() == JsonValueKind.Number ? (p.GetValue<int>() / 100m).ToString("0.00", Da) + " kr. på Steam" : "Steam",
            "https://store.steampowered.com/app/" + OpenLibraryTips.S(d?["id"]),
            OpenLibraryTips.S(d?["tiny_image"]))).Where(t => t.Title.Length > 0).ToList();
    }
}

internal static class HttpJson
{
    public static async Task<JsonNode?> GetFromJsonNode(this HttpClient http, string url, CancellationToken ct) =>
        JsonNode.Parse(await http.GetStringAsync(url, ct));
}

public static class DependencyInjection
{
    /// <summary>JSON files in the data folder, the Kunder check, the free tip APIs.</summary>
    public static IServiceCollection AddFaellesskabInfrastructure(this IServiceCollection services, string dataDir, string? kunderUrl, string? kunderKey)
    {
        services.AddSingleton<ICommunityStore>(new JsonCommunityStore(dataDir));
        services.AddKunderClient(kunderUrl, kunderKey, dataDir);
        services.AddScoped<ICustomerCheck, KunderCustomerCheck>();
        // Scryfall and MusicBrainz ask every caller to say who they are.
        static void Polite(HttpClient c)
        {
            c.Timeout = TimeSpan.FromSeconds(8);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("KolibriFaellesskab/1.0 (+https://faellesskab.itmartin.dk; ITMartin@Mensa.dk)");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }
        services.AddHttpClient<OpenLibraryTips>(Polite);
        services.AddHttpClient<ScryfallTips>(Polite);
        services.AddHttpClient<MusicBrainzTips>(Polite);
        services.AddHttpClient<SteamTips>(Polite);
        services.AddTransient<ITipSource>(sp => sp.GetRequiredService<OpenLibraryTips>());
        services.AddTransient<ITipSource>(sp => sp.GetRequiredService<ScryfallTips>());
        services.AddTransient<ITipSource>(sp => sp.GetRequiredService<MusicBrainzTips>());
        services.AddTransient<ITipSource>(sp => sp.GetRequiredService<SteamTips>());
        return services;
    }
}
