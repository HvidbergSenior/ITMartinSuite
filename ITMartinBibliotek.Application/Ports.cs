using System.Text.RegularExpressions;
using ITMartinBibliotek.Domain;

namespace ITMartinBibliotek.Application;

// ---- What the use cases need from the outside world. Implemented in Infrastructure. ----

public interface IMediaRepository
{
    Task<List<MediaItem>> ForOwnerAsync(string owner);
    Task<List<MediaItem>> AllAsync();
    Task<MediaItem?> GetAsync(Guid id);
    Task<MediaItem?> ByBarcodeAsync(string owner, string barcode);
    Task<bool> OwnsAsync(string owner, string jellyfinId);
    Task AddAsync(MediaItem item);
    Task UpdateAsync(MediaItem item);
    Task DeleteAsync(Guid id);

    // One save for a whole Jellyfin sync: new rows, changed rows and rows that are gone.
    Task ApplySyncAsync(IReadOnlyCollection<MediaItem> added, IReadOnlyCollection<MediaItem> changed, IReadOnlyCollection<MediaItem> removed);
}

// Connection details and API keys, entered once under Indstillinger and kept in the database.
public interface ISettings
{
    Task<string> GetAsync(string key);
    Task<Dictionary<string, string>> GetAllAsync();
    Task SetAsync(string key, string value);
}

public static class SettingKeys
{
    public const string JellyfinUrl = "JellyfinUrl";
    public const string JellyfinApiKey = "JellyfinApiKey";
    public const string JellyfinPublicUrl = "JellyfinPublicUrl";
    public const string TmdbApiKey = "TmdbApiKey";
    public const string DiscogsToken = "DiscogsToken";
    public const string LastSync = "LastSync";
}

// Library = the Jellyfin library (virtual folder) the item sits in; it decides which customer owns it.
public sealed record JellyfinItem(string Id, string Name, string Type, int? Year, string Artist, string TmdbId, string MusicBrainzId, string Collection, string Library = "")
{
    public MediaKind Kind => Type switch
    {
        "Series" => MediaKind.Serie,
        "MusicAlbum" => MediaKind.Musik,
        _ => MediaKind.Film
    };
}

public sealed record PlayerTrack(string Id, string Name, string Artist, int Seconds, int Disc, int Number);

public sealed record PlayerAlbum(string Id, string Name, string Artist, int? Year, string Cover, List<PlayerTrack> Tracks);

// A song found by name, with the album it is on.
public sealed record TrackHit(string Id, string Name, string Artist, string AlbumId, string AlbumName);

public interface IJellyfin
{
    Task<(bool ok, string message)> TestAsync();
    Task<List<JellyfinItem>> ListLibraryAsync();
    Task RefreshLibraryAsync();
    Task RefreshLibraryAsync(string libraryName);
    Task<string> CoverUrlAsync(string jellyfinId);
    Task<string> HomeUrlAsync();
    Task<string> PlayUrlAsync(string jellyfinId);
    Task<PlayerAlbum?> AlbumAsync(string albumId);
    Task<List<TrackHit>> SearchTracksAsync(string query, int limit);
    Task<HttpResponseMessage> OpenAudioAsync(string trackId, string range, CancellationToken ct);
    Task MarkPlayedAsync(string trackId);
}

public static partial class JellyfinIds
{
    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex IdPattern();

    // Jellyfin ids are 32 hex chars; anything else never reaches an API URL.
    public static bool IsId(string id) => IdPattern().IsMatch(id);
}

public sealed record Candidate(
    string Source, string Title, string Artist, int? Year, string CoverUrl,
    MediaKind Kind, MediaFormat? Format,
    string TmdbId = "", string MusicBrainzId = "", string DiscogsId = "");

public interface IMetadataLookup
{
    Task<List<Candidate>> ByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<List<Candidate>> SearchTitleAsync(string query, MediaKind kind, CancellationToken ct = default);
}

// One disc going through the rip station on the PC (ripstation.ps1).
// The station owns the job and posts the whole record on every change.
public sealed class RipJob
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";          // "cd" | "dvd" | "bluray"
    public string Label { get; set; } = "";         // disc volume label as the drive reports it
    public string Title { get; set; } = "";         // what it was saved as
    public string Status { get; set; } = "";        // ripping | copying | converting | finishing | done | failed
    public string Step { get; set; } = "";          // plain-Danish line shown under the title
    public int Progress { get; set; }               // 0-100 for the current step
    public string Target { get; set; } = "";        // NAS folder
    public bool NeedsName { get; set; }             // name guessed from the disc label - check it
    public string Error { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface IRipStatus
{
    event Action? Changed;
    DateTime? LastSeen { get; }
    List<RipJob> Jobs();
    void Heartbeat();
    void Upsert(RipJob job);
    void Remove(string id);
}
