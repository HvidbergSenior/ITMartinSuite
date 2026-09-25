namespace ITMartinBibliotek.Server.Data.Entities;

public enum MediaKind { Film = 0, Serie = 1, Musik = 2 }

public enum MediaFormat { Dvd = 0, BluRay = 1, Cd = 2, Digital = 3 }

// The single question the catalog answers for every disc on the shelf:
// is it only on the shelf, or is it ripped and playable in Jellyfin?
public enum MediaStatus { Ejet = 0, Rippet = 1 }

public sealed class MediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;   // music only

    // Main category for films/series (Anders Matthesen, X-Men...), taken from the
    // folder the title sits in on disk: Film\<Collection>\<Title>\. Music groups by Artist.
    public string Collection { get; set; } = string.Empty;
    public MediaKind Kind { get; set; }
    public MediaFormat Format { get; set; }
    public MediaStatus Status { get; set; }
    public int? Year { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // Provider ids, used to match against Jellyfin's own ProviderIds so a
    // rip is recognised even when the folder name differs from the title.
    public string TmdbId { get; set; } = string.Empty;
    public string MusicBrainzId { get; set; } = string.Empty;
    public string DiscogsId { get; set; } = string.Empty;

    // Set by the Jellyfin sync when a matching library item exists.
    public string JellyfinId { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
