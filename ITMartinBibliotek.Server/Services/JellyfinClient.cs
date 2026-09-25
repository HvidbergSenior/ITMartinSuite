using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITMartinBibliotek.Server.Data.Entities;

namespace ITMartinBibliotek.Server.Services;

public sealed record JellyfinItem(string Id, string Name, string Type, int? Year, string Artist, string TmdbId, string MusicBrainzId, string Collection)
{
    public MediaKind Kind => Type switch
    {
        "Series" => MediaKind.Serie,
        "MusicAlbum" => MediaKind.Musik,
        _ => MediaKind.Film
    };
}

// Thin client over the parts of Jellyfin's REST API the catalog needs:
// list what is in the library, and build image/play links for it.
public sealed class JellyfinClient(IHttpClientFactory httpFactory, SettingsStore settings)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<(bool ok, string message)> TestAsync()
    {
        try
        {
            var http = await ClientAsync();
            var info = await http.GetFromJsonAsync<SystemInfo>("System/Info", Json);
            return (true, $"Forbundet til {info?.ServerName} (Jellyfin {info?.Version})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<List<JellyfinItem>> ListLibraryAsync()
    {
        var http = await ClientAsync();
        const string url = "Items?IncludeItemTypes=Movie,Series,MusicAlbum&Recursive=true&Fields=ProductionYear,ProviderIds,AlbumArtist,Path&Limit=5000";
        var page = await http.GetFromJsonAsync<ItemsPage>(url, Json) ?? new ItemsPage();
        var roots = await LibraryRootsAsync(http);
        return page.Items.Select(i => new JellyfinItem(
            i.Id, i.Name, i.Type, i.ProductionYear, i.AlbumArtist ?? "",
            i.ProviderIds.GetValueOrDefault("Tmdb", ""),
            i.ProviderIds.GetValueOrDefault("MusicBrainzAlbum", i.ProviderIds.GetValueOrDefault("MusicBrainzReleaseGroup", "")),
            i.Type == "MusicAlbum" ? "" : CollectionFromPath(i.Path, i.Type, roots)))
            .ToList();
    }

    // Film\X-Men\X2 (2003)\X2 (2003).mkv -> "X-Men". The title's own folder is skipped
    // (a movie Path is the file, a series Path is its folder); the next folder up is the
    // collection unless it is a library root, i.e. the title is not grouped.
    public static string CollectionFromPath(string? path, string type, IReadOnlySet<string> roots)
    {
        if (string.IsNullOrEmpty(path)) return "";
        var parts = path.Replace('\\', '/').TrimEnd('/').Split('/');
        var up = type == "Series" ? 1 : 2;
        if (parts.Length <= up) return "";
        var parent = string.Join('/', parts[..^up]);
        return roots.Contains(parent) ? "" : parts[^(up + 1)];
    }

    private static async Task<HashSet<string>> LibraryRootsAsync(HttpClient http)
    {
        var folders = await http.GetFromJsonAsync<List<VirtualFolder>>("Library/VirtualFolders", Json) ?? [];
        return folders.SelectMany(f => f.Locations).Select(l => l.Replace('\\', '/').TrimEnd('/')).ToHashSet();
    }

    // Images are unauthenticated in Jellyfin, so the browser can load them straight
    // from the server the user reaches (the public URL when set, else the API URL).
    public async Task<string> CoverUrlAsync(string jellyfinId)
    {
        var b = await PublicBaseAsync();
        return string.IsNullOrEmpty(b) ? "" : $"{b}/Items/{jellyfinId}/Images/Primary?maxWidth=400";
    }

    public async Task<string> HomeUrlAsync()
    {
        var b = await PublicBaseAsync();
        return string.IsNullOrEmpty(b) ? "" : $"{b}/web/";
    }

    public async Task<string> PlayUrlAsync(string jellyfinId)
    {
        var b = await PublicBaseAsync();
        return string.IsNullOrEmpty(b) ? "" : $"{b}/web/index.html#!/details?id={jellyfinId}";
    }

    // Asks Jellyfin to scan its libraries, so a fresh rip shows up without waiting
    // for the scheduled scan.
    public async Task RefreshLibraryAsync()
    {
        var http = await ClientAsync();
        using var res = await http.PostAsync("Library/Refresh", null);
        res.EnsureSuccessStatusCode();
    }

    private async Task<string> PublicBaseAsync()
    {
        var pub = await settings.GetAsync(SettingsStore.JellyfinPublicUrl);
        if (string.IsNullOrWhiteSpace(pub)) pub = await settings.GetAsync(SettingsStore.JellyfinUrl);
        return pub.TrimEnd('/');
    }

    private async Task<HttpClient> ClientAsync()
    {
        var baseUrl = (await settings.GetAsync(SettingsStore.JellyfinUrl)).TrimEnd('/');
        var key = await settings.GetAsync(SettingsStore.JellyfinApiKey);
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Jellyfin-adresse og API-nøgle mangler under Indstillinger.");
        var http = httpFactory.CreateClient("jellyfin");
        http.BaseAddress = new Uri(baseUrl + "/");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("MediaBrowser", $"Token=\"{key}\"");
        return http;
    }

    private sealed class SystemInfo
    {
        public string? ServerName { get; set; }
        public string? Version { get; set; }
    }

    private sealed class VirtualFolder
    {
        public List<string> Locations { get; set; } = [];
    }

    private sealed class ItemsPage
    {
        public List<ItemDto> Items { get; set; } = [];
    }

    private sealed class ItemDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int? ProductionYear { get; set; }
        public string? AlbumArtist { get; set; }
        public string? Path { get; set; }
        public Dictionary<string, string> ProviderIds { get; set; } = [];
    }
}
