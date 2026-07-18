using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITMartinKaraoke.Server.Services;

public sealed record SpotifyTrack(string Id, string Name, string Artist, string? AlbumArtUrl, int DurationMs);

// Own OAuth app/tokens, independent from MusikStudio's SpotifyService - this
// app runs as its own container with its own redirect URI, so a login here
// never touches MusikStudio's stored token and vice versa. Same Authorization
// Code flow (safe to keep the client secret server-side, Blazor Server only).
public sealed class SpotifyService
{
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string ApiBase = "https://api.spotify.com/v1";

    // streaming: required for the Web Playback SDK to create a Connect device.
    private const string Scopes = "streaming user-read-email user-read-private";

    private readonly IHttpClientFactory _httpFactory;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string _redirectUri;
    private readonly string _tokenFilePath;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public SpotifyService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _clientId = config["Spotify:ClientId"];
        _clientSecret = config["Spotify:ClientSecret"];
        _redirectUri = config["Spotify:RedirectUri"] ?? "";
        var dataRoot = Path.GetDirectoryName(
            (config.GetConnectionString("KaraokeDb") ?? "Data Source=/app/data/karaoke.db")
                .Replace("Data Source=", ""))
            ?? "/app/data";
        _tokenFilePath = Path.Combine(dataRoot, "spotify-token.json");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);

    public bool IsConnected => File.Exists(_tokenFilePath);

    public string GetAuthorizeUrl(string state)
    {
        var qs = new Dictionary<string, string>
        {
            ["client_id"] = _clientId!,
            ["response_type"] = "code",
            ["redirect_uri"] = _redirectUri,
            ["scope"] = Scopes,
            ["state"] = state,
        };
        return AuthorizeUrl + "?" + string.Join("&", qs.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<bool> HandleCallbackAsync(string code, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _redirectUri,
            }),
        };
        req.Headers.Authorization = BasicAuthHeader();

        var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return false;

        var token = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        if (token is null) return false;

        await SaveTokenAsync(new StoredToken(token.AccessToken, token.RefreshToken!,
            DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60)));
        return true;
    }

    public async Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            var stored = await LoadTokenAsync();
            if (stored is null) return null;
            if (stored.ExpiresAtUtc > DateTime.UtcNow) return stored.AccessToken;

            var http = _httpFactory.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = stored.RefreshToken,
                }),
            };
            req.Headers.Authorization = BasicAuthHeader();

            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var token = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            if (token is null) return null;

            var newRefresh = token.RefreshToken ?? stored.RefreshToken;
            var updated = new StoredToken(token.AccessToken, newRefresh, DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60));
            await SaveTokenAsync(updated);
            return updated.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<List<SpotifyTrack>> SearchTracksAsync(string query, CancellationToken ct = default)
    {
        var accessToken = await GetValidAccessTokenAsync(ct);
        if (accessToken is null || string.IsNullOrWhiteSpace(query)) return [];

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var url = $"{ApiBase}/search?type=track&limit=10&q={Uri.EscapeDataString(query)}";
        var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return [];

        var result = await resp.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: ct);
        return result?.Tracks?.Items?.Select(t => new SpotifyTrack(
            t.Id,
            t.Name,
            string.Join(", ", t.Artists.Select(a => a.Name)),
            t.Album?.Images?.FirstOrDefault()?.Url,
            t.DurationMs)).ToList() ?? [];
    }

    private AuthenticationHeaderValue BasicAuthHeader()
    {
        var raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
        return new AuthenticationHeaderValue("Basic", raw);
    }

    private async Task<StoredToken?> LoadTokenAsync()
    {
        if (!File.Exists(_tokenFilePath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(_tokenFilePath);
            return JsonSerializer.Deserialize<StoredToken>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveTokenAsync(StoredToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tokenFilePath)!);
        await File.WriteAllTextAsync(_tokenFilePath, JsonSerializer.Serialize(token));
    }

    private sealed record StoredToken(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private sealed class SearchResponse
    {
        [JsonPropertyName("tracks")] public TracksObj? Tracks { get; set; }
        public sealed class TracksObj { [JsonPropertyName("items")] public List<TrackObj>? Items { get; set; } }
        public sealed class TrackObj
        {
            [JsonPropertyName("id")] public string Id { get; set; } = "";
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("duration_ms")] public int DurationMs { get; set; }
            [JsonPropertyName("artists")] public List<ArtistObj> Artists { get; set; } = [];
            [JsonPropertyName("album")] public AlbumObj? Album { get; set; }
        }
        public sealed class ArtistObj { [JsonPropertyName("name")] public string Name { get; set; } = ""; }
        public sealed class AlbumObj { [JsonPropertyName("images")] public List<ImageObj>? Images { get; set; } }
        public sealed class ImageObj { [JsonPropertyName("url")] public string Url { get; set; } = ""; }
    }
}
