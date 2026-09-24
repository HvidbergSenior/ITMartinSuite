using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace ITMartinStrategi.Server.Services;

public sealed record SteamGame(int AppId, string Name, double Hours, double Hours2Weeks);
public sealed record SteamNews(string Title, string Url, DateTime Date, bool FromDevelopers);
public sealed record SteamAchievement(string Name, string Description, double GlobalPercent, bool Achieved);

/// <summary>Martin's Steam account through the free Steam Web API: owned games
/// with hours, news/patch notes per game, and achievements with global rarity.
/// Everything is cached - a page view never waits on Steam more than once an hour.</summary>
public sealed partial class SteamService(IHttpClientFactory http, IConfiguration config, IMemoryCache cache, ILogger<SteamService> logger)
{
    private const string Api = "https://api.steampowered.com";
    private readonly string? _key = config["Steam:ApiKey"];
    private readonly string? _steamId = config["Steam:SteamId"];

    public bool Configured => !string.IsNullOrWhiteSpace(_key) && !string.IsNullOrWhiteSpace(_steamId);

    public static string HeaderImage(int appId) => $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/capsule_231x87.jpg";
    public static string StorePage(int appId) => $"https://store.steampowered.com/app/{appId}/";

    public Task<IReadOnlyList<SteamGame>> OwnedGamesAsync() => Cached("owned", TimeSpan.FromMinutes(30), async () =>
    {
        if (!Configured) return (IReadOnlyList<SteamGame>)[];
        using var doc = await GetAsync($"{Api}/IPlayerService/GetOwnedGames/v1/?key={_key}&steamid={_steamId}&include_appinfo=1&include_played_free_games=1");
        if (doc is null || !doc.RootElement.GetProperty("response").TryGetProperty("games", out var games)) return [];
        return games.EnumerateArray()
            .Select(g => new SteamGame(
                g.GetProperty("appid").GetInt32(),
                g.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Math.Round(g.GetProperty("playtime_forever").GetInt32() / 60.0, 1),
                g.TryGetProperty("playtime_2weeks", out var w) ? Math.Round(w.GetInt32() / 60.0, 1) : 0))
            .Where(g => g.Name.Length > 0)
            .OrderByDescending(g => g.Hours2Weeks).ThenByDescending(g => g.Hours)
            .ToList();
    });

    // No key needed for news. "Community Announcements" are the developers' own posts (patch notes, dev journals).
    public Task<IReadOnlyList<SteamNews>> NewsAsync(int appId) => Cached($"news:{appId}", TimeSpan.FromHours(1), async () =>
    {
        using var doc = await GetAsync($"{Api}/ISteamNews/GetNewsForApp/v2/?appid={appId}&count=15&maxlength=1");
        if (doc is null || !doc.RootElement.GetProperty("appnews").TryGetProperty("newsitems", out var items)) return (IReadOnlyList<SteamNews>)[];
        return items.EnumerateArray()
            .Select(n => new SteamNews(
                n.GetProperty("title").GetString() ?? "",
                n.GetProperty("url").GetString() ?? "",
                DateTimeOffset.FromUnixTimeSeconds(n.GetProperty("date").GetInt64()).UtcDateTime,
                n.TryGetProperty("feedname", out var f) && f.GetString() == "steam_community_announcements"))
            .OrderByDescending(n => n.Date)
            .ToList();
    });

    public Task<IReadOnlyList<SteamAchievement>> AchievementsAsync(int appId) => Cached($"ach:{appId}", TimeSpan.FromHours(1), async () =>
    {
        if (!Configured) return (IReadOnlyList<SteamAchievement>)[];
        using var schema = await GetAsync($"{Api}/ISteamUserStats/GetSchemaForGame/v2/?key={_key}&appid={appId}&l=english");
        using var mine = await GetAsync($"{Api}/ISteamUserStats/GetPlayerAchievements/v1/?key={_key}&steamid={_steamId}&appid={appId}");
        using var global = await GetAsync($"{Api}/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid={appId}");
        if (schema is null || !schema.RootElement.TryGetProperty("game", out var game)
            || !game.TryGetProperty("availableGameStats", out var stats) || !stats.TryGetProperty("achievements", out var list))
            return [];

        var achieved = new HashSet<string>();
        if (mine?.RootElement.GetProperty("playerstats").TryGetProperty("achievements", out var mineList) == true)
            foreach (var a in mineList.EnumerateArray())
                if (a.GetProperty("achieved").GetInt32() == 1) achieved.Add(a.GetProperty("apiname").GetString() ?? "");

        var percent = new Dictionary<string, double>();
        if (global?.RootElement.GetProperty("achievementpercentages").TryGetProperty("achievements", out var gList) == true)
            foreach (var a in gList.EnumerateArray())
                percent[a.GetProperty("name").GetString() ?? ""] = a.GetProperty("percent").ValueKind == JsonValueKind.String
                    ? double.TryParse(a.GetProperty("percent").GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0
                    : a.GetProperty("percent").GetDouble();

        return list.EnumerateArray().Select(a =>
        {
            var api = a.GetProperty("name").GetString() ?? "";
            return new SteamAchievement(
                a.TryGetProperty("displayName", out var d) ? d.GetString() ?? api : api,
                a.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                Math.Round(percent.GetValueOrDefault(api), 1),
                achieved.Contains(api));
        }).ToList();
    });

    private async Task<JsonDocument?> GetAsync(string url)
    {
        try
        {
            using var res = await http.CreateClient("steam").GetAsync(url);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogInformation("Steam {Status} for {Url}", (int)res.StatusCode, KeyPattern().Replace(url, "key=***"));
                return null;
            }
            return await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        }
        catch (Exception ex)
        {
            logger.LogWarning("Steam call failed: {Message}", ex.Message);
            return null;
        }
    }

    private async Task<T> Cached<T>(string key, TimeSpan ttl, Func<Task<T>> load)
    {
        if (cache.TryGetValue(key, out T? hit) && hit is not null) return hit;
        var value = await load();
        cache.Set(key, value, ttl);
        return value;
    }

    [GeneratedRegex("key=[^&]+")]
    private static partial Regex KeyPattern();
}
