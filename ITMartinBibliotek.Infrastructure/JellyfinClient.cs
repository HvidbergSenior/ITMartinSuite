using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITMartinBibliotek.Application;
using ITMartinBibliotek.Domain;

namespace ITMartinBibliotek.Infrastructure;

// Thin client over the parts of Jellyfin's REST API the catalog needs:
// list what is in the library, build image/play links for it, and feed the
// app's own music player (album track lists, audio streams, played marks).
public sealed class JellyfinClient(IHttpClientFactory httpFactory, ISettings settings) : IJellyfin
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // The phone fetches each track in many byte ranges, and every range resolves
    // album + position to a track, so track lists are kept for a while.
    private readonly ConcurrentDictionary<string, (DateTime at, PlayerAlbum album)> _albums = new();
    private string _playedUserId = "";

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
        var folders = await http.GetFromJsonAsync<List<VirtualFolder>>("Library/VirtualFolders", Json) ?? [];
        var roots = folders.SelectMany(f => f.Locations).Select(LibraryPaths.Norm).ToHashSet();
        var libraries = folders.Select(f => (f.Name, f.Locations)).ToList();
        return page.Items.Select(i => new JellyfinItem(
            i.Id, i.Name, i.Type, i.ProductionYear, i.AlbumArtist ?? "",
            i.ProviderIds.GetValueOrDefault("Tmdb", ""),
            i.ProviderIds.GetValueOrDefault("MusicBrainzAlbum", i.ProviderIds.GetValueOrDefault("MusicBrainzReleaseGroup", "")),
            i.Type == "MusicAlbum" ? "" : LibraryPaths.CollectionFromPath(i.Path, i.Type, roots),
            LibraryPaths.LibraryForPath(i.Path, libraries)))
            .ToList();
    }

    // Rescans one library only - a customer's few albums, not the household's whole collection.
    // Their files arrive over NFS from the upload box, which Jellyfin's file watcher never sees.
    public async Task RefreshLibraryAsync(string libraryName)
    {
        var http = await ClientAsync();
        var folders = await http.GetFromJsonAsync<List<VirtualFolder>>("Library/VirtualFolders", Json) ?? [];
        var id = folders.FirstOrDefault(f => string.Equals(f.Name, libraryName, StringComparison.OrdinalIgnoreCase))?.ItemId;
        if (string.IsNullOrEmpty(id)) return;
        using var res = await http.PostAsync($"Items/{id}/Refresh?Recursive=true", null);
        res.EnsureSuccessStatusCode();
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

    public async Task<PlayerAlbum?> AlbumAsync(string albumId)
    {
        if (_albums.TryGetValue(albumId, out var hit) && DateTime.UtcNow - hit.at < TimeSpan.FromMinutes(10))
            return hit.album;
        var http = await ClientAsync();
        var head = (await http.GetFromJsonAsync<ItemsPage>($"Items?Ids={albumId}&Fields=ProductionYear", Json))?.Items.FirstOrDefault();
        if (head is null) return null;
        var page = await http.GetFromJsonAsync<ItemsPage>(
            $"Items?ParentId={albumId}&IncludeItemTypes=Audio&Recursive=true&SortBy=ParentIndexNumber,IndexNumber,SortName", Json) ?? new ItemsPage();
        var b = await PublicBaseAsync();
        var album = new PlayerAlbum(head.Id, head.Name, head.AlbumArtist ?? "", head.ProductionYear,
            b == "" ? "" : $"{b}/Items/{head.Id}/Images/Primary?maxWidth=512",
            page.Items.Select(t => new PlayerTrack(t.Id, t.Name,
                t.Artists.Count > 0 ? string.Join(", ", t.Artists) : head.AlbumArtist ?? "",
                (int)(t.RunTimeTicks / 10_000_000), t.ParentIndexNumber ?? 1, t.IndexNumber ?? 0)).ToList());
        _albums[albumId] = (DateTime.UtcNow, album);
        return album;
    }

    // Songs by name over the whole server; the caller keeps only albums the viewer owns.
    public async Task<List<TrackHit>> SearchTracksAsync(string query, int limit)
    {
        var http = await ClientAsync();
        var page = await http.GetFromJsonAsync<ItemsPage>(
            $"Items?SearchTerm={Uri.EscapeDataString(query)}&IncludeItemTypes=Audio&Recursive=true&Limit={limit}&Fields=AlbumId", Json) ?? new ItemsPage();
        return page.Items.Where(t => !string.IsNullOrEmpty(t.AlbumId))
            .Select(t => new TrackHit(t.Id, t.Name, t.Artists.Count > 0 ? string.Join(", ", t.Artists) : t.AlbumArtist ?? "", t.AlbumId!, t.Album ?? ""))
            .ToList();
    }

    // The original file (FLAC/MP3/M4A) straight from Jellyfin with byte ranges passed
    // through, so the phone can seek and Jellyfin never has to transcode.
    public async Task<HttpResponseMessage> OpenAudioAsync(string trackId, string range, CancellationToken ct)
    {
        var http = await ClientAsync("jellyfin-stream");
        var req = new HttpRequestMessage(HttpMethod.Get, $"Audio/{trackId}/stream?static=true");
        if (!string.IsNullOrEmpty(range)) req.Headers.TryAddWithoutValidation("Range", range);
        return await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    // Played marks go to the Jellyfin administrator (the household's own account), so
    // play counts and "recently played" in Jellyfin also cover what was heard here.
    public async Task MarkPlayedAsync(string trackId)
    {
        var http = await ClientAsync();
        if (_playedUserId == "")
        {
            var users = await http.GetFromJsonAsync<List<UserDto>>("Users", Json) ?? [];
            _playedUserId = users.FirstOrDefault(u => u.Policy?.IsAdministrator == true)?.Id ?? "";
            if (_playedUserId == "") return;
        }
        using var res = await http.PostAsync($"Users/{_playedUserId}/PlayedItems/{trackId}", null);
        res.EnsureSuccessStatusCode();
    }

    private async Task<string> PublicBaseAsync()
    {
        var pub = await settings.GetAsync(SettingKeys.JellyfinPublicUrl);
        if (string.IsNullOrWhiteSpace(pub)) pub = await settings.GetAsync(SettingKeys.JellyfinUrl);
        return pub.TrimEnd('/');
    }

    private async Task<HttpClient> ClientAsync(string name = "jellyfin")
    {
        var baseUrl = (await settings.GetAsync(SettingKeys.JellyfinUrl)).TrimEnd('/');
        var key = await settings.GetAsync(SettingKeys.JellyfinApiKey);
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Jellyfin-adresse og API-nøgle mangler under Indstillinger.");
        var http = httpFactory.CreateClient(name);
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
        public string Name { get; set; } = "";
        public string? ItemId { get; set; }
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
        public List<string> Artists { get; set; } = [];
        public long RunTimeTicks { get; set; }
        public int? IndexNumber { get; set; }
        public int? ParentIndexNumber { get; set; }
        public string? AlbumId { get; set; }
        public string? Album { get; set; }
    }

    private sealed class UserDto
    {
        public string Id { get; set; } = "";
        public UserPolicy? Policy { get; set; }
    }

    private sealed class UserPolicy
    {
        public bool IsAdministrator { get; set; }
    }
}
