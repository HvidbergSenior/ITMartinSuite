using ITMartinLager.Domain;

namespace ITMartinLager.Application;

// What the use cases need from the outside world. Implemented in ITMartinLager.Infrastructure.

/// <summary>One price site (DBA, eBay, AbeBooks ...). Null = the site could not be asked (blocked, down, not set up).</summary>
public interface IPriceSource
{
    string Name { get; }
    /// <summary>A shop's new price, not a used market.</summary>
    bool IsNew { get; }
    /// <summary>Where it is: "Sverige", "Tyskland" ... or "".</summary>
    string Country { get; }
    Task<Listings?> LookupAsync(string title, string author, string isbn, CancellationToken ct);
    /// <summary>The site's own search page for the book, for a person to check.</summary>
    string SearchUrl(string title, string author, string isbn);
    /// <summary>Whether this site has this kind of thing at all (HipComic only comics, Discogs only CDs ...).</summary>
    bool Fits(string kind) => true;
}

/// <summary>Claude: values a pile of books from the price sites' numbers - batched, under a daily cap.</summary>
public interface IBookValuer
{
    bool Enabled { get; }
    int MaxCallsPerDay { get; }
    Task<int> CallsTodayAsync(CancellationToken ct);
    Task<Dictionary<int, BookValue>> ValueBooksAsync(IReadOnlyList<string> lines, CancellationToken ct);
}

/// <summary>Claude: reads piles of covers and kolofons, writes collector texts - batched, under the daily cap.</summary>
public interface ICoverReader
{
    bool Enabled { get; }
    int MaxCallsPerDay { get; }
    Task<int> CallsTodayAsync(CancellationToken ct);
    Task<List<Found>> ReadPileAsync(byte[] jpeg, IReadOnlyList<byte[]> closeUps, string hint, CancellationToken ct);
    Task<Dictionary<int, string>> WriteInterestAsync(IReadOnlyList<string> lines, CancellationToken ct);
    Task<Dictionary<int, KolofonReading>> ReadKolofonsAsync(IReadOnlyList<byte[]> jpegs, CancellationToken ct);
}

/// <summary>Grand Comics Database (2026-10-25): the year of a Danish comic from its series and number. Null = not known for
/// sure (no match, several matches, GCD did not answer).</summary>
public interface IComicCatalog
{
    Task<int?> YearAsync(string series, string number, CancellationToken ct);
}

/// <summary>Barcode -> what it is (Google Books, Open Library, MusicBrainz, UPCitemdb). Null = not found.</summary>
public interface IBarcodeLookup
{
    Task<Found?> LookupAsync(string code, CancellationToken ct);
}

/// <summary>Kunder (user 2026-10-24): is this kundenummer a helper Martin has ticked? Name = who it is; null = no.</summary>
public interface IHelperNumbers
{
    Task<string?> HelperNameAsync(string kundenummer, CancellationToken ct = default);

    /// <summary>Kunder's Admin tick (2026-10-26): the kundenummer opens the app as admin. Name = who it is; null = no.</summary>
    Task<string?> AdminNameAsync(string kundenummer, CancellationToken ct = default) => Task.FromResult<string?>(null);
}

/// <summary>Vareinfo (2026-10-20): the capture app - helpers take photos and scan barcodes box by box, and it keeps
/// them. Lager pulls what is new and fetches a photo when it reads it. Server to server with a shared key.</summary>
public interface IVareinfoGateway
{
    /// <summary>Where a person opens Vareinfo (https://vareinfo.bogshoppen.dk).</summary>
    string PublicBase { get; }
    /// <summary>Photos and scans Lager has not said it has yet, oldest first.</summary>
    Task<CaptureBatch> NewAsync(int max, CancellationToken ct);
    Task ReceivedAsync(CaptureReceived received, CancellationToken ct);
    /// <summary>A photo (tile null) or one of its close-ups. Null = gone.</summary>
    Task<byte[]?> PhotoAsync(int id, int? tile, CancellationToken ct);
}

public interface IIntakeRepository
{
    /// <summary>Keeps the ones not kept yet (by VareinfoId) - fetching the same batch twice changes nothing.</summary>
    Task AddAsync(IEnumerable<BoxShot> shots, IEnumerable<BoxScan> scans, CancellationToken ct);
    /// <summary>Per box with photos or scans, newest box first.</summary>
    Task<List<BoxIntake>> PerBoxAsync(CancellationToken ct);
    /// <summary>Marks the unread photos and scans of one box (null = every box) to be read. Returns how many.</summary>
    Task<int> WantReadAsync(string? boxCode, CancellationToken ct);
    Task<List<BoxShot>> WantedShotsAsync(int max, CancellationToken ct);
    Task<List<BoxScan>> WantedScansAsync(int max, CancellationToken ct);
    Task UpdateAsync(IEnumerable<BoxShot> shots, CancellationToken ct);
    Task UpdateAsync(IEnumerable<BoxScan> scans, CancellationToken ct);
    /// <summary>The photos and scans that came from Vareinfo for this box (a deleted box).</summary>
    Task ForgetBoxAsync(string boxCode, CancellationToken ct);
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

/// <summary>Live Auktion (auction-web), server to server.</summary>
public interface IAuctionGateway
{
    bool Enabled { get; }
    string PublicBase { get; }
    Task<List<AuctionSessionInfo>> SessionsAsync(CancellationToken ct);
    /// <summary>highlight = the guld/sølv/bronze/måske mark shown to bidders, or null.</summary>
    Task<Guid> SendAsync(string code, string name, string description, decimal startPrice, int lot, byte[]? jpeg, string? highlight, CancellationToken ct);
    Task<List<AuctionStatusInfo>> StatusAsync(IEnumerable<Guid> ids, CancellationToken ct);
}

/// <summary>The MTG Scanner (magic-web).</summary>
public interface IMagicGateway
{
    bool Enabled { get; }
    Task<List<MagicOwner>> OwnersAsync(CancellationToken ct);
    Task<List<MagicCardInfo>> CardsAsync(string owner, CancellationToken ct);
}

/// <summary>Settings the use cases read (Lager__* in magic.env).</summary>
public sealed record LagerOptions
{
    public int KolofonFromKr { get; init; } = Kolofon.DefaultFromKr;
    /// <summary>Where a person opens Salg - the orders and the day offers (Lager__SalgPublicUrl).</summary>
    public string SalgPublicUrl { get; init; } = "https://salg.bogshoppen.dk";
}

/// <summary>The sales Salg has booked here (by its order token) - each is taken out of the stock once.</summary>
public interface ISaleBookingRepository
{
    Task<bool> ExistsAsync(string saleRef, CancellationToken ct);
    /// <summary>Saves the sold items and the booking together.</summary>
    Task BookAsync(string saleRef, IEnumerable<Item> soldItems, CancellationToken ct);
}

/// <summary>A message for the person, in Danish - shown as it is.</summary>
public sealed class LagerException(string message) : Exception(message);

/// <summary>Today's kiosk prices per blad series (/kiosk).</summary>
public interface IKioskRepository
{
    Task<List<KioskPrice>> ListAsync(CancellationToken ct = default);
    Task SaveAsync(KioskPrice price, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
