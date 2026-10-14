using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

// What the use cases need from the outside world. Implemented in ITMartinVareinfo.Infrastructure.

/// <summary>Claude: reads piles of covers and kolofons, writes collector texts - batched, under a daily cap.</summary>
public interface ICoverReader
{
    bool Enabled { get; }
    int MaxCallsPerDay { get; }
    Task<int> CallsTodayAsync(CancellationToken ct);
    Task<List<Found>> ReadPileAsync(byte[] jpeg, IReadOnlyList<byte[]> closeUps, string hint, CancellationToken ct);
    Task<Dictionary<int, string>> WriteInterestAsync(IReadOnlyList<string> lines, CancellationToken ct);
    Task<Dictionary<int, KolofonReading>> ReadKolofonsAsync(IReadOnlyList<byte[]> jpegs, CancellationToken ct);
}

/// <summary>Photo files in the data folder. Paths are relative to it.</summary>
public interface IPhotoStore
{
    /// <summary>Phone photos (3-12 MB) -> a JPEG of at most ~1568 px, upright.</summary>
    Task<byte[]> ShrinkAsync(Stream photo, CancellationToken ct);
    /// <summary>Close-ups for reading small text: the full-resolution photo cut in 2x2 overlapping parts, each at most
    /// ~1568 px. Empty when the photo is too small for close-ups to show more than the whole picture.</summary>
    Task<List<byte[]>> TilesAsync(Stream photo, CancellationToken ct);
    Task SaveAsync(string path, byte[] jpeg, CancellationToken ct);
    Task<byte[]?> ReadAsync(string path, CancellationToken ct);
    /// <summary>The full file name when the path is inside the data folder and exists, else null.</summary>
    string? FullPath(string path);
}

/// <summary>Barcode -> what it is (Google Books, Open Library, MusicBrainz, UPCitemdb). Null = not found.</summary>
public interface IBarcodeLookup
{
    Task<Found?> LookupAsync(string code, CancellationToken ct);
}

public interface IPhotoRepository
{
    Task<Photo> AddAsync(Photo photo, CancellationToken ct);
    Task<Photo?> GetAsync(int id, CancellationToken ct);
    Task UpdateAsync(Photo photo, CancellationToken ct);
    /// <summary>Box photos the person asked to have read, oldest first.</summary>
    Task<List<Photo>> WantedAsync(int max, CancellationToken ct);
    /// <summary>Marks the waiting photos of one box (null = every box) to be read. Returns how many.</summary>
    Task<int> WantReadAsync(string? boxCode, CancellationToken ct);
    /// <summary>Per box with box photos, newest box first.</summary>
    Task<List<BoxPhotos>> BoxesAsync(CancellationToken ct);
}

/// <summary>A box on Lager (Code K0007 = NR 7 on the box).</summary>
public sealed record LagerBox(int Id, string Code)
{
    public int Number => BoxNumbers.Of(Code);
}

public interface IKolofonShotRepository
{
    Task AddAsync(KolofonShot shot, CancellationToken ct);
    Task<List<KolofonShot>> WaitingAsync(int max, CancellationToken ct);
    Task<int> WaitingCountAsync(CancellationToken ct);
    Task<List<KolofonShot>> RecentlyReadAsync(int max, CancellationToken ct);
    /// <summary>The newest shot for a Lager item (for its photo).</summary>
    Task<KolofonShot?> LatestForAsync(int itemId, CancellationToken ct);
    Task UpdateAsync(IEnumerable<KolofonShot> shots, CancellationToken ct);
}

/// <summary>What Lager already has of a thing: 0 = new. ItemId = the item to open in Lager.</summary>
public sealed record InStock(int Quantity, int? ItemId, string? Gold = null, string? GoldUrl = null);

/// <summary>A book on Lager's kolofon list: the cover may be a first edition, or a copy sells for FromKr or more.</summary>
public sealed record KolofonWanted(int ItemId, string BoxCode, string Line, string Title, bool FirstEditionLook, int TopKr);

/// <summary>Lager (storage and pricing), server to server with a shared key. Everything Vareinfo reads ends up there.</summary>
public interface ILagerGateway
{
    string PublicBase { get; }
    /// <summary>Saves the products in stock (no box - user 2026-10-12). Same thing, same condition = a higher quantity.</summary>
    /// <summary>addTo = items Lager already has: Quantity more copies ("+1 eksemplar", 0 = "Samme") and a scanned
    /// barcode the item did not have yet.</summary>
    Task<(int Added, int Merged)> SaveAsync(int? photoId, IReadOnlyList<Found> found, IReadOnlyList<(int ItemId, int Quantity, string Barcode)> addTo, CancellationToken ct);
    /// <summary>What a box photo held, into that box (same thing, same condition in the box = a higher quantity).</summary>
    Task<(int Added, int Merged)> SaveInBoxAsync(string boxCode, int photoId, IReadOnlyList<Found> found, CancellationToken ct);
    /// <summary>The newest box, or null when Lager has none.</summary>
    Task<LagerBox?> LatestBoxAsync(CancellationToken ct);
    /// <summary>The next box (K0001, K0002 ...).</summary>
    Task<LagerBox> NewBoxAsync(CancellationToken ct);
    /// <summary>For each thing, in order: how many Lager already has of exactly that.</summary>
    Task<List<InStock>> InStockAsync(IReadOnlyList<Found> found, CancellationToken ct);
    Task<(int FromKr, List<KolofonWanted> Books)> KolofonWantedAsync(CancellationToken ct);
    /// <summary>A kolofon photo was taken - Lager takes the book off the list and shows "venter på at blive læst".</summary>
    Task KolofonTakenAsync(int itemId, CancellationToken ct);
    /// <summary>What was read (null = unreadable) - Lager puts edition, publisher, year and ISBN on the book.</summary>
    Task KolofonReadAsync(int itemId, KolofonReading? reading, CancellationToken ct);
}

/// <summary>Settings the use cases read (Vareinfo__* in magic.env).</summary>
public sealed record VareinfoOptions
{
    /// <summary>Write the collector text right after each photo (Vareinfo__AutoInterest, off by default - the costly part).</summary>
    public bool AutoInterest { get; init; }
}

/// <summary>A message for the person, in Danish - shown as it is.</summary>
public sealed class VareinfoException(string message) : Exception(message);
